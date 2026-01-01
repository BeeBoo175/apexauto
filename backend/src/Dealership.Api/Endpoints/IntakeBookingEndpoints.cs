using System.IdentityModel.Tokens.Jwt;
using Dealership.Application.Intake;

namespace Dealership.Api.Endpoints;

public static class IntakeBookingEndpoints
{
    public static void MapIntakeBookingEndpoints(this WebApplication app)
    {
        var routes = app.MapGroup("/api/intake/appointments").RequireAuthorization();
        routes.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            return await next(context);
        });
        routes.MapPost("", BookAsync);
        routes.MapGet("/{id:guid}", GetReceiptAsync);
    }

    private static async Task<IResult> BookAsync(HttpContext context, IntakeBookingService bookings,
        CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));

        try
        {
            var request = await JsonRequestReader.ReadAsync<BookingRequest>(context.Request, deadline.Token);

            if (request.Error is not null)
            {
                return request.Error;
            }

            return Map(await bookings.BookAsync(OwnerId(context), request.Value!, deadline.Token));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Results.Problem(statusCode: 503, title: "Booking is unconfirmed; reconcile using the same booking ID and contents.");
        }
    }

    private static async Task<IResult> GetReceiptAsync(Guid id, HttpContext context, IntakeBookingService bookings,
        CancellationToken cancellationToken)
    {
        return Map(await bookings.GetReceiptAsync(OwnerId(context), id, cancellationToken));
    }

    private static Guid OwnerId(HttpContext context) => Guid.Parse(context.User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);

    private static IResult Map(BookingResult result)
    {
        return result.Status switch
        {
            BookingStatus.Succeeded => Results.Ok(result.Receipt),
            BookingStatus.Forbidden => Results.Forbid(),
            BookingStatus.NotFound => Results.NotFound(),
            BookingStatus.Invalid => Results.Problem(statusCode: 400, title: "Provide valid vehicle, registration, quote and four photo IDs."),
            BookingStatus.QuoteChanged => Results.Json(result.FreshQuote, statusCode: 409),
            BookingStatus.Conflict => Results.Problem(statusCode: 409, title: "Booking ID already belongs to a different request."),
            BookingStatus.SlotUnavailable => Results.Problem(statusCode: 409, title: "The branch or inspection slot is unavailable."),
            BookingStatus.PhotosUnavailable => Results.Problem(statusCode: 409, title: "Provide four available, unexpired and unused owned photo angles."),
            _ => Results.Problem(statusCode: 500)
        };
    }
}

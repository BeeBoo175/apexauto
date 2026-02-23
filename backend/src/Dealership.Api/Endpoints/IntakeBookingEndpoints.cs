using Dealership.Application.Common;
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
        routes.MapGet("", ListAsync);
        routes.MapGet("/{id:guid}", GetReceiptAsync);
        routes.MapPost("/{id:guid}/cancellation", CancelAsync);
    }

    private static async Task<IResult> CancelAsync(Guid id, HttpContext context, AppointmentCancellationService cancellations,
        CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));

        try
        {
            var result = await cancellations.CancelAsync(OwnerId(context), id, deadline.Token);
            return result.Status switch
            {
                CancellationStatus.Succeeded => Results.Ok(result.Receipt),
                CancellationStatus.Forbidden => Results.Forbid(),
                CancellationStatus.NotFound => Results.NotFound(),
                CancellationStatus.TooLate => Results.Problem(statusCode: 409, title: "The appointment has already started."),
                CancellationStatus.CheckedIn => Results.Problem(statusCode: 409, title: "The appointment is already checked in."),
                _ => Results.BadRequest()
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Results.Problem(statusCode: 503, title: "Cancellation is unconfirmed; reconcile using the same appointment ID.");
        }
    }

    private static async Task<IResult> ListAsync(HttpContext context, IAppointmentHistoryStore history,
        CancellationToken cancellationToken, string? from = null, string? to = null, int page = 1, int pageSize = Pagination.DefaultPageSize)
    {
        var query = AppointmentHistoryQuery.Parse(from, to, page, pageSize);

        if (query is null)
        {
            return Results.Problem(statusCode: 400, title: "Provide a valid paired date range of at most 366 days and valid paging.");
        }

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));

        try
        {
            var result = await history.ListAsync(OwnerId(context), query, deadline.Token);
            return result is null ? Results.Forbid() : Results.Ok(result);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Results.Problem(statusCode: 503, title: "Appointment history is temporarily unavailable.");
        }
    }

    private static async Task<IResult> BookAsync(HttpContext context, IntakeBookingService bookings,
        CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));

        try
        {
            var (request, error) = await JsonRequestReader.ReadAsync<BookingRequest>(context.Request, deadline.Token);

            if (error is not null)
            {
                return error;
            }

            return Map(await bookings.BookAsync(OwnerId(context), request!, deadline.Token));
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

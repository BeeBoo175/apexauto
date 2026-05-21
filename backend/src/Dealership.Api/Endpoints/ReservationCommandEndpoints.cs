using System.IdentityModel.Tokens.Jwt;
using Dealership.Application.Vehicles;

namespace Dealership.Api.Endpoints;

public static class ReservationCommandEndpoints
{
    public static void MapReservationCommandEndpoints(this WebApplication app)
    {
        var routes = app.MapGroup("/api").RequireAuthorization();
        routes.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            return await next(context);
        });
        routes.MapPost("/vehicles/{vehicleId:guid}/reservations", ReserveAsync);
        routes.MapPost("/reservations/{reservationId:guid}/cancellations", CancelAsync);
    }

    private static async Task<IResult> ReserveAsync(Guid vehicleId, HttpContext context, ReservationService reservations,
        CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            var (request, error) = await JsonRequestReader.ReadAsync<ReservationRequest>(context.Request, deadline.Token);
            if (error is not null)
            {
                return error;
            }

            var result = await reservations.ReserveAsync(ActorId(context), vehicleId, request!, deadline.Token);
            var receipt = result.Reservation is null ? null : ReservationReceipt.From(result.Reservation);
            return Map(result.Status, receipt);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Unconfirmed();
        }
    }

    private static async Task<IResult> CancelAsync(Guid reservationId, HttpContext context,
        ReservationCancellationService cancellations, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            var (request, error) = await JsonRequestReader.ReadAsync<ReservationCancellationRequest>(context.Request, deadline.Token);
            if (error is not null)
            {
                return error;
            }

            var result = await cancellations.CancelAsync(ActorId(context), reservationId, request!, deadline.Token);
            var receipt = result.Release is null ? null : ReservationRefundReceipt.From(result.Release);
            return Map(result.Status, receipt);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Unconfirmed();
        }
    }

    private static Guid ActorId(HttpContext context) => Guid.Parse(context.User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);

    private static IResult Unconfirmed() => Results.Problem(statusCode: 503,
        title: "Reservation operation is unconfirmed; reconcile using the same operation ID and contents.");

    private static IResult Map(ReservationStatus status, object? receipt) => status switch
    {
        ReservationStatus.Succeeded => Results.Ok(receipt),
        ReservationStatus.Invalid => Results.Problem(statusCode: 400, title: "Provide valid reservation details and an operation ID."),
        ReservationStatus.Forbidden => Results.Forbid(),
        ReservationStatus.NotFound => Results.NotFound(),
        ReservationStatus.Conflict => Results.Problem(statusCode: 409, title: "The reservation or operation has changed or is unavailable."),
        ReservationStatus.CustomerHasReservation => Results.Problem(statusCode: 409,
            title: "You already have an active reservation.",
            extensions: new Dictionary<string, object?> { ["code"] = "customer-has-reservation" }),
        ReservationStatus.VehicleReserved => Results.Problem(statusCode: 409,
            title: "This car is reserved by another customer.",
            extensions: new Dictionary<string, object?> { ["code"] = "vehicle-reserved" }),
        ReservationStatus.PromoUnavailable => Results.Problem(statusCode: 409, title: "The accepted promo is unavailable."),
        ReservationStatus.Unavailable => Results.Problem(statusCode: 503, title: "Reservation terms are temporarily unavailable."),
        _ => Results.Problem(statusCode: 500)
    };
}

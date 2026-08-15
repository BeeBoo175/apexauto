using Dealership.Api.Configuration;
using Dealership.Application.Accounts;
using Dealership.Application.Vehicles;
using static Dealership.Api.Endpoints.HttpContextActor;

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
        routes.MapPost("/vehicles/{vehicleId:guid}/reservation-requests", RequestDepositAsync);
        routes.MapPost("/reservations/{reservationId:guid}/cancellations", CancelAsync);
        routes.MapPost("/reservation-requests/{requestId:guid}/cancellations", CancelDepositRequestAsync);
        // The customer follows the link in the deposit request mail, so confirming needs no sign-in.
        app.MapPost("/api/reservation-deposits/confirmations", ConfirmDepositAsync)
            .RequireRateLimiting(ApiRateLimiting.VerificationPolicy).NoStore();
    }

    private static async Task<IResult> RequestDepositAsync(Guid vehicleId, HttpContext context,
        ReservationDepositRequestService deposits, IStaffAssistanceQuery assistance, CancellationToken cancellationToken)
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

            var owner = await AssistedOwner.ResolveAsync(context, assistance, deadline.Token);
            if (owner.Error is not null)
            {
                return owner.Error;
            }

            // Only staff ask for a deposit; a customer reserves and pays for themselves.
            if (owner.Staff is null)
            {
                return Results.Forbid();
            }

            var result = await deposits.RequestAsync(owner.OwnerId, vehicleId, request!, owner.Staff, deadline.Token);
            var receipt = result.Request is null ? null : ReservationDepositRequestReceipt.From(result.Request);
            return Map(result.Status, receipt);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Unconfirmed();
        }
    }

    private static async Task<IResult> CancelDepositRequestAsync(Guid requestId, HttpContext context,
        ReservationDepositRequestService deposits, IStaffAssistanceQuery assistance, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            var staff = await assistance.FindStaffAsync(ActorId(context), deadline.Token);
            if (staff is null)
            {
                return Results.Forbid();
            }

            return await deposits.CancelAsync(requestId, staff, deadline.Token) switch
            {
                ReservationStatus.Succeeded => Results.Ok(new ReservationDepositRequestCancellationReceipt(requestId)),
                ReservationStatus.Forbidden => Results.Forbid(),
                ReservationStatus.NotFound => Results.NotFound(),
                _ => Results.Problem(statusCode: 409, title: "The deposit request is no longer pending.")
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Unconfirmed();
        }
    }

    private static async Task<IResult> ConfirmDepositAsync(HttpContext context, ReservationDepositRequestService deposits,
        CancellationToken cancellationToken)
    {
        var (request, error) = await JsonRequestReader.ReadAsync<DepositConfirmationRequest>(context.Request, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        var result = await deposits.ConfirmAsync(request!.Token, cancellationToken);
        return result.Status switch
        {
            ReservationStatus.Succeeded => Results.NoContent(),
            ReservationStatus.Invalid or ReservationStatus.NotFound => Results.Problem(statusCode: 400,
                title: "The deposit link is invalid."),
            ReservationStatus.DepositRequestLapsed => Results.Problem(statusCode: 410,
                title: "The deposit link has expired and the hold has been released."),
            _ => Results.Problem(statusCode: 409, title: "The deposit can no longer be confirmed for this car.")
        };
    }

    private sealed record DepositConfirmationRequest(string? Token);

    private static async Task<IResult> ReserveAsync(Guid vehicleId, HttpContext context, ReservationService reservations,
        IStaffAssistanceQuery assistance, CancellationToken cancellationToken)
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

            var owner = await AssistedOwner.ResolveAsync(context, assistance, deadline.Token);
            if (owner.Error is not null)
            {
                return owner.Error;
            }

            var result = await reservations.ReserveAsync(owner.OwnerId, vehicleId, request!, deadline.Token, owner.Staff);
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

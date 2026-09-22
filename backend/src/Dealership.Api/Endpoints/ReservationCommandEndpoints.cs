using Dealership.Api.Configuration;
using Dealership.Application.Accounts;
using Dealership.Application.Vehicles;
using static Dealership.Api.Endpoints.HttpContextActor;

namespace Dealership.Api.Endpoints;

public static class ReservationCommandEndpoints
{
    public static void MapReservationCommandEndpoints(this WebApplication app)
    {
        var routes = app.MapGroup("/api").RequireAuthorization().NoStore();
        routes.MapPost("/vehicles/{vehicleId:guid}/reservations", ReserveAsync);
        routes.MapPost("/vehicles/{vehicleId:guid}/reservation-requests", RequestDepositAsync);
        routes.MapPost("/reservations/{reservationId:guid}/cancellations", CancelAsync);
        routes.MapPost("/reservation-requests/{requestId:guid}/cancellations", CancelDepositRequestAsync);
        routes.MapPost("/account/pending-payments/{requestId:guid}/cancellations", CancelOwnPaymentAsync);
        // The customer follows the link in the deposit request mail, so confirming needs no sign-in.
        app.MapPost("/api/reservation-deposits/confirmations", ConfirmDepositAsync)
            .RequireRateLimiting(ApiRateLimiting.VerificationPolicy).NoStore();
        // The browser returns from the hosted payment page here; only the stored state is read, never the gateway.
        app.MapGet("/api/reservation-deposits/{requestId:guid}/status", DepositStatusAsync)
            .RequireRateLimiting(ApiRateLimiting.VerificationPolicy).NoStore();
    }

    private static async Task<IResult> RequestDepositAsync(Guid vehicleId, HttpContext context,
        ReservationDepositRequestService deposits, IStaffAssistanceQuery assistance, CancellationToken cancellationToken)
    {
        return await BoundedCommand.RunAsync(async deadline =>
        {
            var (request, error) = await JsonRequestReader.ReadAsync<ReservationRequest>(context.Request, deadline);
            if (error is not null)
            {
                return error;
            }

            var owner = await AssistedOwner.ResolveAsync(context, assistance, deadline);
            if (owner.Error is not null)
            {
                return owner.Error;
            }

            // Only staff ask for a deposit; a customer reserves and pays for themselves.
            if (owner.Staff is null)
            {
                return Results.Forbid();
            }

            var result = await deposits.RequestAsync(owner.OwnerId, vehicleId, request!, owner.Staff, deadline);
            var receipt = result.Request is null ? null : ReservationDepositRequestReceipt.From(result.Request);
            return Map(result.Status, receipt);
        }, Unconfirmed, cancellationToken);
    }

    private static async Task<IResult> CancelDepositRequestAsync(Guid requestId, HttpContext context,
        ReservationPaymentService payments, IStaffAssistanceQuery assistance, CancellationToken cancellationToken)
    {
        return await BoundedCommand.RunAsync(async deadline =>
        {
            var staff = await assistance.FindStaffAsync(ActorId(context), deadline);
            if (staff is null)
            {
                return Results.Forbid();
            }

            var cancelled = await payments.CancelAsync(requestId, staff, deadline);
            return cancelled switch
            {
                ReservationStatus.Succeeded => Results.Ok(new ReservationDepositRequestCancellationReceipt(requestId)),
                ReservationStatus.Forbidden => Results.Forbid(),
                ReservationStatus.NotFound => Results.NotFound(),
                ReservationStatus.DepositAlreadyPaid => AlreadyPaid(),
                _ => Results.Problem(statusCode: 409, title: "The deposit request is no longer pending.")
            };
        }, Unconfirmed, cancellationToken);
    }

    private static async Task<IResult> CancelOwnPaymentAsync(Guid requestId, HttpContext context,
        ReservationPaymentService payments, CancellationToken cancellationToken)
    {
        return await BoundedCommand.RunAsync(async deadline =>
        {
            var cancelled = await payments.CancelOwnAsync(ActorId(context), requestId, deadline);
            return cancelled switch
            {
                ReservationStatus.Succeeded => Results.Ok(new ReservationDepositRequestCancellationReceipt(requestId)),
                ReservationStatus.NotFound => Results.NotFound(),
                ReservationStatus.DepositAlreadyPaid => AlreadyPaid(),
                _ => Results.Problem(statusCode: 409, title: "The payment is no longer pending.")
            };
        }, Unconfirmed, cancellationToken);
    }

    private static IResult AlreadyPaid() => Results.Problem(statusCode: 409,
        title: "The deposit was already paid, so the reservation stays.",
        extensions: new Dictionary<string, object?> { ["code"] = "deposit-already-paid" });

    private static async Task<IResult> ConfirmDepositAsync(HttpContext context, ReservationPaymentService payments,
        CancellationToken cancellationToken)
    {
        var (request, error) = await JsonRequestReader.ReadAsync<DepositConfirmationRequest>(context.Request, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        var result = await payments.StartAsync(request!.Token, cancellationToken);
        return result.Status switch
        {
            DepositPaymentStatus.Confirmed => Results.NoContent(),
            DepositPaymentStatus.PaymentRequired => Results.Ok(new DepositPaymentReceipt(result.PaymentUrl)),
            DepositPaymentStatus.Waiting => Results.Accepted(),
            DepositPaymentStatus.Invalid => Results.Problem(statusCode: 400, title: "The deposit link is invalid."),
            DepositPaymentStatus.Lapsed => Results.Problem(statusCode: 410,
                title: "The deposit link has expired and the hold has been released."),
            DepositPaymentStatus.Unavailable => Results.Problem(statusCode: 503,
                title: "The payment service is temporarily unavailable."),
            _ => Results.Problem(statusCode: 409, title: "The deposit can no longer be confirmed for this car.")
        };
    }

    private static async Task<IResult> DepositStatusAsync(Guid requestId, ReservationPaymentService payments,
        CancellationToken cancellationToken)
    {
        var result = await payments.StatusAsync(requestId, cancellationToken);
        return result.Status == DepositPaymentStatus.Invalid
            ? Results.NotFound()
            : Results.Ok(new DepositPaymentStatusReceipt(PaymentCallbackEndpoints.StatusName(result.Status)));
    }

    private sealed record DepositPaymentReceipt(string? PaymentUrl);

    internal sealed record DepositPaymentStatusReceipt(string Status);

    private sealed record DepositConfirmationRequest(string? Token);

    private static async Task<IResult> ReserveAsync(Guid vehicleId, HttpContext context, ReservationService reservations,
        ReservationDepositRequestService deposits, ReservationPaymentService payments,
        IStaffAssistanceQuery assistance, CancellationToken cancellationToken)
    {
        return await BoundedCommand.RunAsync(async deadline =>
        {
            var (request, error) = await JsonRequestReader.ReadAsync<ReservationRequest>(context.Request, deadline);
            if (error is not null)
            {
                return error;
            }

            var owner = await AssistedOwner.ResolveAsync(context, assistance, deadline);
            if (owner.Error is not null)
            {
                return owner.Error;
            }

            // Staff reserve for a customer only by asking for a deposit, never by creating the reservation directly.
            if (owner.Staff is not null)
            {
                return Results.Forbid();
            }

            // With the payment gateway on, a customer's own reservation holds the car for a short payment window and
            // sends them to pay; the reservation exists once the signed payment is confirmed.
            if (payments.IsEnabled)
            {
                return await PayForReservationAsync(owner.OwnerId, vehicleId, request!, reservations, deposits, payments,
                    deadline);
            }

            var result = await reservations.ReserveAsync(owner.OwnerId, vehicleId, request!, deadline);
            var receipt = result.Reservation is null ? null : ReservationReceipt.From(result.Reservation);
            return Map(result.Status, receipt);
        }, Unconfirmed, cancellationToken);
    }

    private static async Task<IResult> PayForReservationAsync(Guid customerId, Guid vehicleId, ReservationRequest request,
        ReservationService reservations, ReservationDepositRequestService deposits, ReservationPaymentService payments,
        CancellationToken cancellationToken)
    {
        var held = await deposits.RequestOwnAsync(customerId, vehicleId, request, cancellationToken);
        if (held.Status != ReservationStatus.Succeeded || held.Request is null)
        {
            return Map(held.Status, null);
        }

        var payment = await payments.StartForRequestAsync(held.Request, cancellationToken);
        switch (payment.Status)
        {
            case DepositPaymentStatus.PaymentRequired:
                return Results.Ok(new DepositPaymentReceipt(payment.PaymentUrl));
            case DepositPaymentStatus.Confirmed:
                // Paid already: the replay of the same operation returns the reservation receipt.
                var reserved = await reservations.ReserveAsync(customerId, vehicleId, request, cancellationToken);
                return Map(reserved.Status, reserved.Reservation is null ? null : ReservationReceipt.From(reserved.Reservation));
            case DepositPaymentStatus.Lapsed:
                return Results.Problem(statusCode: 410, title: "The payment window has ended and the hold was released.");
            case DepositPaymentStatus.Unavailable:
                return Results.Problem(statusCode: 503, title: "The payment service is temporarily unavailable.");
            default:
                return Results.Problem(statusCode: 409, title: "The reservation or operation has changed or is unavailable.");
        }
    }

    private static async Task<IResult> CancelAsync(Guid reservationId, HttpContext context,
        ReservationCancellationService cancellations, CancellationToken cancellationToken)
    {
        return await BoundedCommand.RunAsync(async deadline =>
        {
            var (request, error) = await JsonRequestReader.ReadAsync<ReservationCancellationRequest>(context.Request, deadline);
            if (error is not null)
            {
                return error;
            }

            var result = await cancellations.CancelAsync(ActorId(context), reservationId, request!, deadline);
            var receipt = result.Release is null ? null : ReservationRefundReceipt.From(result.Release);
            return Map(result.Status, receipt);
        }, Unconfirmed, cancellationToken);
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

using Dealership.Application.Accounts;
using Dealership.Domain.Vehicles;

namespace Dealership.Application.Vehicles;

// A deposit request holds the car until its deposit is paid: staff ask the customer by email and the hold lasts 24
// hours, while a customer reserving for themselves has a short payment window. With the payment gateway off the
// customer confirms a simulated deposit instead. Confirmation makes the ordinary reservation and sends its receipt.
public sealed class ReservationDepositRequestService(IReservationStore reservations,
    IReservationDepositRequestStore requests, ReservationService reservation, TimeProvider clock)
{
    public Task<DepositRequestResult> RequestAsync(Guid customerId, Guid vehicleId, ReservationRequest request,
        AssistingStaff assistedBy, CancellationToken cancellationToken)
    {
        return CreateAsync(customerId, vehicleId, request, assistedBy, cancellationToken);
    }

    // A customer reserving for themselves holds the car through a short payment window instead of waiting on mail.
    public Task<DepositRequestResult> RequestOwnAsync(Guid customerId, Guid vehicleId, ReservationRequest request,
        CancellationToken cancellationToken)
    {
        return CreateAsync(customerId, vehicleId, request, null, cancellationToken);
    }

    private async Task<DepositRequestResult> CreateAsync(Guid customerId, Guid vehicleId, ReservationRequest request,
        AssistingStaff? assistedBy, CancellationToken cancellationToken)
    {
        if (customerId == Guid.Empty || vehicleId == Guid.Empty || request.OperationId == Guid.Empty ||
            request.PublicationId == Guid.Empty || request.PromoId == Guid.Empty)
        {
            return new(ReservationStatus.Invalid);
        }

        await using var transaction = await reservations.LockAsync(customerId, vehicleId, request, cancellationToken);
        var context = transaction.Context;
        if (context.Customer is not { IsActive: true, EmailConfirmed: true, Role: AccountRoles.Customer } ||
            context.Profile is null)
        {
            return new(ReservationStatus.Forbidden);
        }

        var existing = context.ExistingRequest;
        if (existing is not null)
        {
            var matches = existing.CustomerId == customerId && existing.VehicleId == vehicleId &&
                existing.PublicationId == request.PublicationId && existing.PromoId == request.PromoId;
            return await CompleteAsync(transaction, matches ? ReservationStatus.Succeeded : ReservationStatus.Conflict,
                matches ? existing : null, cancellationToken);
        }

        // The operation ID is also the reservation's, so one already used as a reservation or release is taken.
        if (context.Existing is not null || context.IsReleaseOperation)
        {
            return await CompleteAsync(transaction, ReservationStatus.Conflict, null, cancellationToken);
        }

        if (context.CustomerHasHold)
        {
            return await CompleteAsync(transaction, ReservationStatus.CustomerHasReservation, null, cancellationToken);
        }

        if (context.VehicleHasHold)
        {
            return await CompleteAsync(transaction, ReservationStatus.VehicleReserved, null, cancellationToken);
        }

        if (context.Publication is null || context.Listing is null)
        {
            return await CompleteAsync(transaction, ReservationStatus.NotFound, null, cancellationToken);
        }

        if (assistedBy is not null && !assistedBy.CanActAt(context.Publication.BranchId))
        {
            return await CompleteAsync(transaction, ReservationStatus.Forbidden, null, cancellationToken);
        }

        if (context.Publication.Id != request.PublicationId)
        {
            return await CompleteAsync(transaction, ReservationStatus.Conflict, null, cancellationToken);
        }

        if (context.Settings is null)
        {
            return await CompleteAsync(transaction, ReservationStatus.Unavailable, null, cancellationToken);
        }

        if (request.PromoId is not null && (context.Promo is null ||
            !context.Promo.CanApply(context.Listing.VehiclePrice, context.Now)))
        {
            return await CompleteAsync(transaction, ReservationStatus.PromoUnavailable, null, cancellationToken);
        }

        var deposit = new ReservationDepositRequest(request.OperationId, context.Profile, context.Publication,
            request.PromoId, assistedBy?.Id, context.Now);
        var status = await transaction.CommitRequestAsync(deposit, cancellationToken);
        return new(status, status == ReservationStatus.Succeeded ? deposit : null);
    }

    public async Task<ReservationResult> ConfirmAsync(string? token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 160)
        {
            return new(ReservationStatus.Invalid);
        }

        var request = await requests.FindByTokenAsync(token, cancellationToken);
        if (request is null)
        {
            return new(ReservationStatus.Invalid);
        }

        return await ConfirmRequestAsync(request, null, cancellationToken);
    }

    // The gateway reported the deposit paid, so the request confirms exactly as the simulated link does. The
    // provider's payment time, not the time its notice arrived, decides whether the request was still holding the car.
    public async Task<ReservationResult> ConfirmPaidAsync(Guid requestId, DateTimeOffset? paidAt,
        CancellationToken cancellationToken)
    {
        var request = requestId == Guid.Empty ? null : await requests.FindAsync(requestId, cancellationToken);
        return request is null
            ? new(ReservationStatus.Invalid)
            : await ConfirmRequestAsync(request, paidAt, cancellationToken);
    }

    private async Task<ReservationResult> ConfirmRequestAsync(ReservationDepositRequest request,
        DateTimeOffset? paidAt, CancellationToken cancellationToken)
    {
        // A request that was cancelled or lapsed unconfirmed can no longer be confirmed unless it was paid before it
        // ran out; a confirmed one replays its reservation.
        var heldWhenPaid = request.IsPending(clock.GetUtcNow()) || (paidAt is { } paid &&
            request.CancelledAt is null && paid >= request.CreatedAt && paid < request.ExpiresAt);
        if (request.ConfirmedAt is null && !heldWhenPaid)
        {
            return new(ReservationStatus.DepositRequestLapsed);
        }

        // The request's own ID is the reservation's operation ID, so a repeated click replays one reservation.
        var staff = request.BookedByStaffId is { } staffId ? AssistingStaff.Recorded(staffId, request.BranchId) : null;
        var result = await reservation.ReserveAsync(request.CustomerId, request.VehicleId,
            new(request.Id, request.PublicationId, request.PromoId), cancellationToken, staff);
        if (result.Status == ReservationStatus.Succeeded)
        {
            await requests.MarkConfirmedAsync(request.Id, clock.GetUtcNow(), cancellationToken);
        }

        return result;
    }

    // Staff release a car they asked a deposit for, within the branch limits that let them ask.
    public async Task<ReservationStatus> CancelAsync(Guid requestId, AssistingStaff staff,
        CancellationToken cancellationToken)
    {
        var request = requestId == Guid.Empty ? null : await requests.FindAsync(requestId, cancellationToken);
        if (request is null)
        {
            return ReservationStatus.NotFound;
        }

        if (!staff.CanActAt(request.BranchId))
        {
            return ReservationStatus.Forbidden;
        }

        // Repeating a cancellation is harmless; a confirmed or lapsed request has nothing left to release.
        if (request.CancelledAt is not null)
        {
            return ReservationStatus.Succeeded;
        }

        return await requests.CancelAsync(requestId, clock.GetUtcNow(), cancellationToken)
            ? ReservationStatus.Succeeded
            : ReservationStatus.DepositRequestLapsed;
    }

    private static async Task<DepositRequestResult> CompleteAsync(IReservationTransaction transaction,
        ReservationStatus status, ReservationDepositRequest? existing, CancellationToken cancellationToken)
    {
        // Reconciliation done while locking must persist even when the request is refused or replayed.
        var committed = await transaction.CommitRequestAsync(null, cancellationToken);
        return committed == ReservationStatus.Succeeded ? new(status, existing) : new(committed);
    }
}

using Dealership.Application.Accounts;
using Dealership.Domain.Vehicles;

namespace Dealership.Application.Vehicles;

public sealed class ReservationCancellationService(IReservationCancellationStore cancellations)
{
    public async Task<ReservationCancellationResult> CancelAsync(Guid actorId, Guid reservationId,
        ReservationCancellationRequest request, CancellationToken cancellationToken)
    {
        if (actorId == Guid.Empty || reservationId == Guid.Empty || request.OperationId == Guid.Empty ||
            request.Reason is not null && (string.IsNullOrWhiteSpace(request.Reason) ||
                request.Reason != request.Reason.Trim() || request.Reason.Length > 1000 || request.Reason.Any(char.IsControl)))
        {
            return new(ReservationStatus.Invalid);
        }

        await using var transaction = await cancellations.LockAsync(actorId, reservationId, request.OperationId, cancellationToken);
        var context = transaction.Context;
        var actor = context.Actor;
        if (actor is not { IsActive: true } ||
            actor.Role is not (AccountRoles.Customer or AccountRoles.BranchManager or AccountRoles.GeneralManager) ||
            actor.Role == AccountRoles.BranchManager && (actor.BranchId is null || !context.BranchActive))
        {
            return new(ReservationStatus.Forbidden);
        }

        var reservation = context.Reservation;
        if (reservation is null || actor.Role == AccountRoles.Customer && reservation.CustomerId != actorId ||
            actor.Role == AccountRoles.BranchManager && reservation.BranchId != actor.BranchId)
        {
            return new(ReservationStatus.NotFound);
        }

        var customerCancellation = actor.Role == AccountRoles.Customer;
        if (customerCancellation != (request.Reason is null))
        {
            return new(ReservationStatus.Invalid);
        }

        var kind = customerCancellation ? ReservationReleaseKind.CustomerCancelled : ReservationReleaseKind.StaffCancelled;
        var existing = context.Existing;
        if (existing is not null)
        {
            var matches = existing.ReservationId == reservationId && existing.ActorId == actorId &&
                existing.Kind == kind && existing.Reason == request.Reason;
            var committed = await transaction.CommitAsync(null, cancellationToken);
            if (committed != ReservationStatus.Succeeded)
            {
                return new(committed);
            }

            return matches ? new(ReservationStatus.Succeeded, existing) : new(ReservationStatus.Conflict);
        }

        if (context.IsHoldOperation || context.Release is not null || !reservation.IsActive)
        {
            await transaction.CommitAsync(null, cancellationToken);
            return new(ReservationStatus.Conflict);
        }

        // A claimed hold has a deal in progress; its agent or a manager cancels that deal, which refunds the deposit.
        if (reservation.IsClaimed)
        {
            await transaction.CommitAsync(null, cancellationToken);
            return new(ReservationStatus.Conflict);
        }

        if (reservation.HasExpired(context.Now))
        {
            var expired = new ReservationRelease(Guid.NewGuid(), reservation, ReservationReleaseKind.Expired,
                null, null, context.Now);
            await transaction.CommitAsync(expired, cancellationToken);
            return new(ReservationStatus.Conflict);
        }

        var release = new ReservationRelease(request.OperationId, reservation, kind, actorId, request.Reason, context.Now);
        var status = await transaction.CommitAsync(release, cancellationToken);
        return new(status, status == ReservationStatus.Succeeded ? release : null);
    }
}

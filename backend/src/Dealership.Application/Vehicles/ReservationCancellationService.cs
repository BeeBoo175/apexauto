using Dealership.Application.Accounts;
using Dealership.Domain.Sales;
using Dealership.Domain.Vehicles;

namespace Dealership.Application.Vehicles;

// A customer cancels their own hold online while nobody handles its sale; staff cancel through the sale instead.
public sealed class ReservationCancellationService(IReservationCancellationStore cancellations)
{
    public async Task<ReservationCancellationResult> CancelAsync(Guid actorId, Guid reservationId,
        ReservationCancellationRequest request, CancellationToken cancellationToken)
    {
        if (actorId == Guid.Empty || reservationId == Guid.Empty || request.OperationId == Guid.Empty)
        {
            return new(ReservationStatus.Invalid);
        }

        await using var transaction = await cancellations.LockAsync(actorId, reservationId, request.OperationId, cancellationToken);
        var context = transaction.Context;
        if (context.Actor is not { IsActive: true, Role: AccountRoles.Customer })
        {
            return new(ReservationStatus.Forbidden);
        }

        // Customers give no reason; a staff reason belongs to cancelling the sale.
        if (request.Reason is not null)
        {
            return new(ReservationStatus.Invalid);
        }

        var reservation = context.Reservation;
        if (reservation is null || reservation.CustomerId != actorId)
        {
            return new(ReservationStatus.NotFound);
        }

        var existing = context.Existing;
        if (existing is not null)
        {
            var matches = existing.ReservationId == reservationId && existing.ActorId == actorId &&
                existing.Kind == ReservationReleaseKind.CustomerCancelled;
            var committed = await transaction.CommitAsync(null, null, cancellationToken);
            if (committed != ReservationStatus.Succeeded)
            {
                return new(committed);
            }

            return matches ? new(ReservationStatus.Succeeded, existing) : new(ReservationStatus.Conflict);
        }

        // Once an agent handles the sale the customer agrees changes with them, so it is not cancelled online.
        var sale = context.Sale;
        if (context.IsHoldOperation || context.Release is not null || !reservation.IsActive ||
            sale is not { Status: SalesOrderStatus.Open })
        {
            await transaction.CommitAsync(null, null, cancellationToken);
            return new(ReservationStatus.Conflict);
        }

        var release = new ReservationRelease(request.OperationId, reservation, sale.DueAt,
            ReservationReleaseKind.CustomerCancelled, actorId, null, context.Now);
        var status = await transaction.CommitAsync(release,
            () => sale.CancelByCustomer(Guid.NewGuid(), actorId, release.Id, context.Now), cancellationToken);
        return new(status, status == ReservationStatus.Succeeded ? release : null);
    }
}
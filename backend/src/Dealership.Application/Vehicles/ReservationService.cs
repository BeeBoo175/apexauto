using Dealership.Application.Accounts;
using Dealership.Domain.Vehicles;

namespace Dealership.Application.Vehicles;

public sealed class ReservationService(IReservationStore reservations)
{
    public async Task<ReservationResult> ReserveAsync(Guid customerId, Guid vehicleId, ReservationRequest request,
        CancellationToken cancellationToken)
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

        var existing = context.Existing;
        if (existing is not null)
        {
            var matches = existing.CustomerId == customerId && existing.VehicleId == vehicleId &&
                existing.PublicationId == request.PublicationId && existing.PromoId == request.PromoId;
            return await CompleteAsync(transaction, matches ? ReservationStatus.Succeeded : ReservationStatus.Conflict,
                matches ? existing : null, cancellationToken);
        }

        if (context.IsReleaseOperation || context.CustomerHasHold || context.VehicleHasHold)
        {
            return await CompleteAsync(transaction, ReservationStatus.Conflict, null, cancellationToken);
        }

        if (context.Publication is null || context.Listing is null)
        {
            return await CompleteAsync(transaction, ReservationStatus.NotFound, null, cancellationToken);
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

        var reservation = new Reservation(request.OperationId, context.Profile, context.Customer.Email!,
            context.Publication, context.Listing, context.Settings, context.Promo, context.Now);
        var status = await transaction.CommitAsync(reservation, cancellationToken);
        return new(status, status == ReservationStatus.Succeeded ? reservation : null);
    }

    private static async Task<ReservationResult> CompleteAsync(IReservationTransaction transaction,
        ReservationStatus status, Reservation? existing, CancellationToken cancellationToken)
    {
        // Reconciliation must persist even when a new hold conflicts or replays an old receipt.
        var committed = await transaction.CommitAsync(null, cancellationToken);
        return committed == ReservationStatus.Succeeded ? new(status, existing) : new(committed);
    }
}

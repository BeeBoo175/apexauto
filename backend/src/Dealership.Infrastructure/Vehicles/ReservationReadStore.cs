using Dealership.Application.Accounts;
using Dealership.Application.Common;
using Dealership.Application.Vehicles;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Vehicles;

public sealed class ReservationReadStore(DealershipDbContext database) : IReservationReadStore
{
    public async Task<ReservationHistoryResult> AccountAsync(Guid customerId, int page, CancellationToken cancellationToken)
    {
        if (!Pagination.IsValid(page, Pagination.DefaultPageSize))
        {
            return new(ReservationStatus.Invalid);
        }

        return await ReservationReadSnapshot.ReadAsync(database,
            () => ReadSnapshotAsync(customerId, page, cancellationToken),
            new ReservationHistoryResult(ReservationStatus.Unavailable), cancellationToken);
    }

    private async Task<ReservationHistoryResult> ReadSnapshotAsync(Guid customerId, int page,
        CancellationToken cancellationToken)
    {
        var actor = await database.Users.AsNoTracking()
            .Where(user => user.Id == customerId && user.IsActive && user.Role == AccountRoles.Customer)
            .Select(user => new CurrentActor(user.Id, user.Role, user.BranchId)).SingleOrDefaultAsync(cancellationToken);
        if (actor is null)
        {
            return new(ReservationStatus.Forbidden);
        }

        // A customer has at most one active hold, so reconciling it keeps every page and the active card current.
        var reservations = database.Reservations.AsNoTracking().Where(item => item.CustomerId == customerId);
        var activeVehicle = await reservations.Where(item => item.IsActive).Select(item => (Guid?)item.VehicleId)
            .SingleOrDefaultAsync(cancellationToken);
        if (activeVehicle is { } vehicleId)
        {
            await ReservationReadSnapshot.ReconcileAsync(database, vehicleId, cancellationToken);
        }

        var rows = ReservationReadRows.CreateRows(database, reservations);
        var total = await rows.CountAsync(cancellationToken);
        var pageIds = await ReservationReadRows.Order(rows).Select(row => row.Reservation.Id)
            .ReadPageAsync(page, Pagination.DefaultPageSize, total, cancellationToken);
        var items = (await ReservationReadRows.Order(rows.Where(row => pageIds.Contains(row.Reservation.Id)))
            .ToArrayAsync(cancellationToken)).Select(ReservationReadRows.ToItem).ToArray();
        var activeRow = await rows.Where(row => row.Reservation.IsActive).SingleOrDefaultAsync(cancellationToken);
        var active = activeRow is null ? null : ReservationReadRows.ToItem(activeRow);
        return new(ReservationStatus.Succeeded,
            new(actor, Array.AsReadOnly(items), total, page, Pagination.DefaultPageSize, active));
    }
}

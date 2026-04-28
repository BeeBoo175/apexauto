using Dealership.Application.Accounts;
using Dealership.Application.Vehicles;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Vehicles;

public sealed class ReservationReadStore(DealershipDbContext database) : IReservationReadStore
{
    public Task<ReservationHistoryResult> AccountAsync(Guid customerId, int page, CancellationToken cancellationToken)
    {
        return ReadAsync(customerId, new(Page: page), false, cancellationToken);
    }

    public Task<ReservationHistoryResult> StaffAsync(Guid actorId, ReservationReadFilter filter, CancellationToken cancellationToken)
    {
        return ReadAsync(actorId, filter, true, cancellationToken);
    }

    public async Task<ReservationDetailResult> DetailAsync(Guid actorId, Guid reservationId, CancellationToken cancellationToken)
    {
        if (reservationId == Guid.Empty)
        {
            return new(ReservationStatus.Invalid);
        }

        return await ReservationReadSnapshot.ReadAsync(database,
            () => DetailSnapshotAsync(actorId, reservationId, cancellationToken),
            new ReservationDetailResult(ReservationStatus.Unavailable), cancellationToken);
    }

    private async Task<ReservationDetailResult> DetailSnapshotAsync(Guid actorId, Guid reservationId,
        CancellationToken cancellationToken)
    {
        var actor = await ReadActorAsync(actorId, null, true, cancellationToken);
        if (actor is null)
        {
            return new(ReservationStatus.Forbidden);
        }

        var reservations = database.Reservations.AsNoTracking().Where(item => item.Id == reservationId &&
            (actor.BranchId == null || item.BranchId == actor.BranchId));
        var target = await reservations.Select(item => new { item.VehicleId, item.IsActive })
            .SingleOrDefaultAsync(cancellationToken);
        if (target is null)
        {
            return new(ReservationStatus.NotFound);
        }

        if (target.IsActive)
        {
            await ReservationReadSnapshot.ReconcileAsync(database, target.VehicleId, cancellationToken);
        }

        var row = await ReservationReadRows.CreateRows(database, reservations).SingleAsync(cancellationToken);
        return new(ReservationStatus.Succeeded, actor, ReservationReadRows.ToItem(row, true));
    }

    private async Task<ReservationHistoryResult> ReadAsync(Guid actorId, ReservationReadFilter filter, bool staff,
        CancellationToken cancellationToken)
    {
        if (!filter.IsValid)
        {
            return new(ReservationStatus.Invalid);
        }

        return await ReservationReadSnapshot.ReadAsync(database,
            () => ReadSnapshotAsync(actorId, filter, staff, cancellationToken),
            new ReservationHistoryResult(ReservationStatus.Unavailable), cancellationToken);
    }

    private async Task<ReservationHistoryResult> ReadSnapshotAsync(Guid actorId, ReservationReadFilter filter, bool staff,
        CancellationToken cancellationToken)
    {
        var actor = await ReadActorAsync(actorId, filter.BranchId, staff, cancellationToken);
        if (actor is null)
        {
            return new(ReservationStatus.Forbidden);
        }

        var reservations = database.Reservations.AsNoTracking().AsQueryable();
        if (!staff)
        {
            reservations = reservations.Where(item => item.CustomerId == actorId);
        }
        else
        {
            var branchId = actor.BranchId ?? filter.BranchId;
            reservations = reservations.Where(item => branchId == null || item.BranchId == branchId);
        }

        if (staff && (filter.IsActive is not null || filter.Sort is "status" or "released" ||
            filter.Status.Length > 0 || filter.ReleasedFrom is not null))
        {
            // Membership and status order depend on refunds across the authorized scope, including off-page holds.
            var caughtUp = await ReservationReadSnapshot.ReconcileDueAsync(database,
                reservations.Select(item => item.VehicleId), 10, cancellationToken);
            if (!caughtUp)
            {
                return new(ReservationStatus.Unavailable);
            }
        }

        ReservationCounts? counts = null;
        if (staff && filter.IsActive is not null)
        {
            // The due holds above are already reconciled, so both tab totals are current.
            counts = new ReservationCounts(
                await reservations.CountAsync(item => item.IsActive, cancellationToken),
                await reservations.CountAsync(item => !item.IsActive, cancellationToken),
                await reservations.CountAsync(item => item.IsActive && item.ClaimedById == null, cancellationToken),
                await reservations.CountAsync(item => item.IsActive && item.ClaimedById != null, cancellationToken));
            reservations = reservations.Where(item => item.IsActive == filter.IsActive);
            if (filter.Claimed is { } claimed)
            {
                reservations = reservations.Where(item => (item.ClaimedById != null) == claimed);
            }
        }

        var filtered = ReservationReadRows.Filter(ReservationReadRows.CreateRows(database, reservations), filter);
        var total = await filtered.CountAsync(cancellationToken);
        var pageIds = await ReservationReadRows.Order(filtered, filter)
            .Select(row => row.Reservation.Id).ReadPageAsync(filter.Page, filter.PageSize, total, cancellationToken);
        var activeVehicles = reservations.Where(item => item.IsActive &&
                (!staff || filter.IsActive == null && filter.Sort != "status" && pageIds.Contains(item.Id)))
            .Select(item => item.VehicleId);
        var vehicleIds = await database.Vehicles.Where(vehicle => activeVehicles.Contains(vehicle.Id))
            .OrderBy(vehicle => vehicle.Id).Select(vehicle => vehicle.Id).ToArrayAsync(cancellationToken);
        // PostgreSQL UUID ordering matches commands; the helper never locks another Customer.
        foreach (var vehicleId in vehicleIds)
        {
            await ReservationReadSnapshot.ReconcileAsync(database, vehicleId, cancellationToken);
        }

        var rows = await ReservationReadRows.Order(filtered.Where(row => pageIds.Contains(row.Reservation.Id)), filter)
            .ToArrayAsync(cancellationToken);
        var items = rows.Select(row => ReservationReadRows.ToItem(row, staff)).ToArray();
        ReservationHistoryItem? active = null;
        if (!staff)
        {
            var activeRow = await ReservationReadRows.CreateRows(database, reservations.Where(item => item.IsActive)).SingleOrDefaultAsync(cancellationToken);
            active = activeRow is null ? null : ReservationReadRows.ToItem(activeRow, false);
        }

        return new(ReservationStatus.Succeeded, new(actor, Array.AsReadOnly(items), total, filter.Page, filter.PageSize, active, counts));
    }

    private async Task<CurrentActor?> ReadActorAsync(Guid actorId, Guid? selectedBranch, bool staff,
        CancellationToken cancellationToken)
    {
        var actor = await database.Users.AsNoTracking().Where(user => user.Id == actorId && user.IsActive)
            .Select(user => new CurrentActor(user.Id, user.Role, user.BranchId)).SingleOrDefaultAsync(cancellationToken);
        if (!staff)
        {
            return actor?.Role == AccountRoles.Customer ? actor : null;
        }

        if (actor?.Role == AccountRoles.GeneralManager)
        {
            return actor;
        }

        if (actor?.Role != AccountRoles.BranchManager || actor.BranchId is null ||
            selectedBranch is not null && selectedBranch != actor.BranchId ||
            !await database.Branches.AnyAsync(branch => branch.Id == actor.BranchId && branch.IsActive, cancellationToken))
        {
            return null;
        }

        return actor;
    }

}

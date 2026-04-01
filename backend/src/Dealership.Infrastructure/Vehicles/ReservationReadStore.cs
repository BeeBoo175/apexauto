using System.Linq.Expressions;
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

        if (staff && (filter.IsActive is not null || filter.Sort == "status"))
        {
            // Membership and status order depend on refunds across the authorized scope, including off-page holds.
            var caughtUp = await ReservationReadSnapshot.ReconcileDueAsync(database,
                reservations.Select(item => item.VehicleId), 10, cancellationToken);
            if (!caughtUp)
            {
                return new(ReservationStatus.Unavailable);
            }
        }

        if (staff && filter.IsActive is not null)
        {
            reservations = reservations.Where(item => item.IsActive == filter.IsActive);
        }

        var total = await reservations.CountAsync(cancellationToken);
        var pageIds = await Order(Rows(reservations), filter)
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

        var rows = await Order(Rows(reservations.Where(item => pageIds.Contains(item.Id))), filter)
            .ToArrayAsync(cancellationToken);
        var items = rows.Select(ToItem).ToArray();
        ReservationHistoryItem? active = null;
        if (!staff)
        {
            var activeRow = await Rows(reservations.Where(item => item.IsActive)).SingleOrDefaultAsync(cancellationToken);
            active = activeRow is null ? null : ToItem(activeRow);
        }

        return new(ReservationStatus.Succeeded, new(actor, Array.AsReadOnly(items), total, filter.Page, filter.PageSize, active));
    }

    private IQueryable<Row> Rows(IQueryable<Reservation> reservations)
    {
        return from reservation in reservations
               join branch in database.Branches on reservation.BranchId equals branch.Id
               join vehicle in database.Vehicles on reservation.VehicleId equals vehicle.Id
               join refunded in database.ReservationReleases.AsNoTracking() on reservation.Id equals refunded.ReservationId into releases
               from release in releases.DefaultIfEmpty()
               select new Row
               {
                   Reservation = reservation,
                   BranchName = branch.Name,
                   Make = vehicle.Make,
                   Model = vehicle.Model,
                   Trim = vehicle.Trim,
                   ManufactureYear = vehicle.ManufactureYear,
                   Release = release
               };
    }

    private static IOrderedQueryable<Row> Order(IQueryable<Row> rows, ReservationReadFilter filter)
    {
        if (filter.Sort.Length == 0)
        {
            return rows.OrderByDescending(row => row.Reservation.CreatedAt).ThenByDescending(row => row.Reservation.Id);
        }

        var ordered = filter.Sort switch
        {
            "reference" => OrderBy(rows, row => row.Reservation.Id, filter.Direction),
            "vehicle" => OrderBy(rows, row => row.ManufactureYear + " " + row.Make + " " + row.Model + " " + row.Trim, filter.Direction),
            "buyer" => OrderBy(rows, row => row.Reservation.Buyer.LegalName, filter.Direction),
            "branch" => OrderBy(rows, row => row.BranchName, filter.Direction),
            "created" => OrderBy(rows, row => row.Reservation.CreatedAt, filter.Direction),
            "expires" => OrderBy(rows, row => row.Reservation.ExpiresAt, filter.Direction),
            "total" => OrderBy(rows, row => row.Reservation.Price.Total, filter.Direction),
            _ => OrderStatus(rows, filter.Direction)
        };
        return ordered.ThenBy(row => row.Reservation.Id);
    }

    private static IOrderedQueryable<Row> OrderBy<TKey>(IQueryable<Row> rows,
        Expression<Func<Row, TKey>> key, string direction)
    {
        return direction == "desc" ? rows.OrderByDescending(key) : rows.OrderBy(key);
    }

    private static IOrderedQueryable<Row> OrderStatus(IQueryable<Row> rows, string direction)
    {
        // Active precedes the alphabetically persisted release kinds when ascending.
        return direction == "desc"
            ? rows.OrderByDescending(row => row.Release != null).ThenByDescending(row => row.Release!.Kind)
            : rows.OrderBy(row => row.Release != null).ThenBy(row => row.Release!.Kind);
    }

    private static ReservationHistoryItem ToItem(Row row) => new(ReservationReceipt.From(row.Reservation), row.BranchName,
        row.Make, row.Model, row.Trim, row.ManufactureYear, row.Reservation.IsActive,
        row.Release is null ? null : ReservationRefundReceipt.From(row.Release));

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

    private sealed class Row
    {
        public required Reservation Reservation { get; init; }
        public required string BranchName { get; init; }
        public required string Make { get; init; }
        public required string Model { get; init; }
        public required string Trim { get; init; }
        public int ManufactureYear { get; init; }
        public required ReservationRelease? Release { get; init; }
    }
}

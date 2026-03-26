using System.Data;
using Dealership.Application.Accounts;
using Dealership.Application.Vehicles;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

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

        // Reconciliation writes can race another release; retry the whole snapshot, never part of it.
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                return await ReadSnapshotAsync(actorId, filter, staff, cancellationToken);
            }
            catch (Exception exception) when (IsSerializationFailure(exception))
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        return new(ReservationStatus.Unavailable);
    }

    private async Task<ReservationHistoryResult> ReadSnapshotAsync(Guid actorId, ReservationReadFilter filter, bool staff,
        CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
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

        var total = await reservations.CountAsync(cancellationToken);
        var pageIds = await reservations.OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id)
            .Select(item => item.Id).ReadPageAsync(filter.Page, filter.PageSize, total, cancellationToken);
        var activeVehicles = reservations.Where(item => item.IsActive && (!staff || pageIds.Contains(item.Id)))
            .Select(item => item.VehicleId);
        var vehicleIds = await database.Vehicles.Where(vehicle => activeVehicles.Contains(vehicle.Id))
            .OrderBy(vehicle => vehicle.Id).Select(vehicle => vehicle.Id).ToArrayAsync(cancellationToken);
        // PostgreSQL UUID ordering matches commands; the helper never locks another Customer.
        foreach (var vehicleId in vehicleIds)
        {
            await database.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM \"Vehicles\" WHERE \"Id\" = {vehicleId} FOR UPDATE", cancellationToken);
            await database.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM \"Reservations\" WHERE \"VehicleId\" = {vehicleId} AND \"IsActive\" FOR UPDATE", cancellationToken);
            await database.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT release_unavailable_reservation({vehicleId})", cancellationToken);
        }

        var rows = await Rows(reservations.Where(item => pageIds.Contains(item.Id)))
            .ToArrayAsync(cancellationToken);
        var items = rows.Select(ToItem).ToArray();
        ReservationHistoryItem? active = null;
        if (!staff)
        {
            var activeRow = await Rows(reservations.Where(item => item.IsActive)).SingleOrDefaultAsync(cancellationToken);
            active = activeRow is null ? null : ToItem(activeRow);
        }

        await transaction.CommitAsync(cancellationToken);
        return new(ReservationStatus.Succeeded, new(actor, Array.AsReadOnly(items), total, filter.Page, filter.PageSize, active));
    }

    private IQueryable<Row> Rows(IQueryable<Reservation> reservations)
    {
        return from reservation in reservations
               join branch in database.Branches on reservation.BranchId equals branch.Id
               join vehicle in database.Vehicles on reservation.VehicleId equals vehicle.Id
               join refunded in database.ReservationReleases.AsNoTracking() on reservation.Id equals refunded.ReservationId into releases
               from release in releases.DefaultIfEmpty()
               orderby reservation.CreatedAt descending, reservation.Id descending
               select new Row(reservation, branch.Name, vehicle.Make, vehicle.Model, vehicle.Trim,
                   vehicle.ManufactureYear, release);
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

    private static bool IsSerializationFailure(Exception exception)
    {
        if (exception is PostgresException { SqlState: PostgresErrorCodes.SerializationFailure })
        {
            return true;
        }

        return exception.InnerException is not null && IsSerializationFailure(exception.InnerException);
    }

    private sealed record Row(Reservation Reservation, string BranchName, string Make, string Model, string Trim,
        int ManufactureYear, ReservationRelease? Release);
}

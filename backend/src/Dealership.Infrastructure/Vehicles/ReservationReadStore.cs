using Dealership.Application.Accounts;
using Dealership.Application.Common;
using Dealership.Application.Vehicles;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Vehicles;

public sealed class ReservationReadStore(DealershipDbContext database) : IReservationReadStore
{
    public async Task<ReservationHistoryResult> AccountAsync(Guid customerId, int page, CancellationToken cancellationToken,
        ListDateRange? dates = null, string? search = null)
    {
        if (!Pagination.IsValidAccount(page, ReservationHistory.PageSize))
        {
            return new(ReservationStatus.Invalid);
        }

        return await ReservationReadSnapshot.ReadAsync(database,
            () => ReadSnapshotAsync(customerId, page, dates, search, cancellationToken),
            new ReservationHistoryResult(ReservationStatus.Unavailable), cancellationToken);
    }

    private async Task<ReservationHistoryResult> ReadSnapshotAsync(Guid customerId, int page, ListDateRange? dates,
        string? search, CancellationToken cancellationToken)
    {
        var actor = await database.Users.AsNoTracking()
            .Where(user => user.Id == customerId && user.IsActive && user.Role == AccountRoles.Customer)
            .Select(user => new CurrentActor(user.Id, user.Role, user.BranchId, user.EmailConfirmed)).SingleOrDefaultAsync(cancellationToken);
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

        var allRows = ReservationReadRows.CreateRows(database, reservations);
        var rows = dates is { From: { } from, Until: { } until }
            ? allRows.Where(row => row.Reservation.CreatedAt >= from && row.Reservation.CreatedAt < until)
            : allRows;
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = LiteralSearch.Pattern(search.Trim());
            rows = rows.Where(row => EF.Functions.ILike(row.Reservation.Number, pattern, "!") ||
                EF.Functions.ILike(row.BranchName, pattern, "!") ||
                EF.Functions.ILike(row.ManufactureYear + " " + row.Make + " " + row.Model + " " + row.Trim, pattern, "!"));
        }
        var total = await rows.CountAsync(cancellationToken);
        var pageIds = await ReservationReadRows.Order(rows).Select(row => row.Reservation.Id)
            .ReadPageAsync(page, ReservationHistory.PageSize, total, cancellationToken);
        var items = (await ReservationReadRows.Order(rows.Where(row => pageIds.Contains(row.Reservation.Id)))
            .ToArrayAsync(cancellationToken)).Select(ReservationReadRows.ToItem).ToArray();
        var activeRow = await allRows.Where(row => row.Reservation.IsActive).SingleOrDefaultAsync(cancellationToken);
        var active = activeRow is null ? null : ReservationReadRows.ToItem(activeRow);
        return new(ReservationStatus.Succeeded,
            new(actor, Array.AsReadOnly(items), total, page, ReservationHistory.PageSize, active));
    }
}

using Dealership.Application.Accounts;
using Dealership.Application.Common;
using Dealership.Application.Vehicles;
using Dealership.Domain.Sales;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Vehicles;

public sealed class ReservationReadStore(DealershipDbContext database, TimeProvider clock) : IReservationReadStore
{
    public async Task<ReservationHistoryResult> AccountAsync(Guid customerId, int page, CancellationToken cancellationToken,
        ListDateRange? dates = null, string? search = null, bool collectedOnly = false)
    {
        if (!Pagination.IsValidAccount(page, ReservationHistory.PageSize))
        {
            return new(ReservationStatus.Invalid);
        }

        return await ReservationReadSnapshot.ReadAsync(database,
            () => ReadSnapshotAsync(customerId, page, dates, search, collectedOnly, cancellationToken),
            new ReservationHistoryResult(ReservationStatus.Unavailable), cancellationToken);
    }

    private async Task<ReservationHistoryResult> ReadSnapshotAsync(Guid customerId, int page, ListDateRange? dates,
        string? search, bool collectedOnly, CancellationToken cancellationToken)
    {
        var actor = await database.Users.AsNoTracking()
            .Where(user => user.Id == customerId && user.IsActive && user.Role == AccountRoles.Customer)
            .Select(ActorRows.Current).SingleOrDefaultAsync(cancellationToken);
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
        var collected = allRows.Where(row => row.Sale.Status == SalesOrderStatus.Delivered);
        var rows = collectedOnly ? collected : allRows;
        if (dates is { From: { } from, Until: { } until })
        {
            rows = collectedOnly
                ? rows.Where(row => row.Sale.DeliveredAt >= from && row.Sale.DeliveredAt < until)
                : rows.Where(row => row.Reservation.CreatedAt >= from && row.Reservation.CreatedAt < until);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = LiteralSearch.Pattern(search.Trim());
            rows = rows.Where(row => EF.Functions.ILike(row.Reservation.Number, pattern, "!") ||
                EF.Functions.ILike(row.BranchName, pattern, "!") ||
                EF.Functions.ILike(row.ManufactureYear + " " + row.Make + " " + row.Model + " " + row.Trim, pattern, "!"));
        }
        var total = await rows.CountAsync(cancellationToken);
        Func<IQueryable<ReservationReadRows.Row>, IOrderedQueryable<ReservationReadRows.Row>> order =
            collectedOnly ? ReservationReadRows.OrderByCollection : ReservationReadRows.Order;
        var pageIds = await order(rows).Select(row => row.Reservation.Id)
            .ReadPageAsync(page, ReservationHistory.PageSize, total, cancellationToken);
        var items = (await order(rows.Where(row => pageIds.Contains(row.Reservation.Id)))
            .ToArrayAsync(cancellationToken)).Select(ReservationReadRows.ToItem).ToArray();
        var collectedTotal = await collected.CountAsync(cancellationToken);
        var activeRow = await allRows.Where(row => row.Reservation.IsActive).SingleOrDefaultAsync(cancellationToken);
        var active = activeRow is null ? null : ReservationReadRows.ToItem(activeRow);
        var pending = await PendingPaymentAsync(customerId, cancellationToken);
        var awaiting = (await allRows.Where(row => row.Sale.Status == SalesOrderStatus.Settled)
                .OrderBy(row => row.Sale.SettledAt).ThenBy(row => row.Reservation.Id)
                .ToArrayAsync(cancellationToken))
            .Select(ReservationReadRows.ToItem).ToArray();
        return new(ReservationStatus.Succeeded,
            new(actor, Array.AsReadOnly(items), total, page, ReservationHistory.PageSize, active, pending)
            {
                AwaitingCollection = Array.AsReadOnly(awaiting),
                CollectedTotal = collectedTotal
            });
    }

    // Includes a request staff made: it holds the car just as a self-service one does and blocks any other reservation.
    private async Task<PendingPaymentItem?> PendingPaymentAsync(Guid customerId, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        return await (from request in database.ReservationDepositRequests.AsNoTracking()
                          .Where(ReservationDepositRequest.PendingAt(now))
                          .Where(item => item.CustomerId == customerId)
                      join branch in database.Branches on request.BranchId equals branch.Id
                      join vehicle in database.Vehicles on request.VehicleId equals vehicle.Id
                      orderby request.CreatedAt
                      select new PendingPaymentItem(request.Id, request.VehicleId, branch.Name, vehicle.Make, vehicle.Model,
                          vehicle.Trim, vehicle.ManufactureYear, request.ExpiresAt,
                          request.BookedByStaffId != null,
                          database.ReservationDepositPayments
                              .Where(payment => payment.RequestId == request.Id)
                              .Select(payment => payment.BillUrl)
                              .FirstOrDefault()))
            .FirstOrDefaultAsync(cancellationToken);
    }
}

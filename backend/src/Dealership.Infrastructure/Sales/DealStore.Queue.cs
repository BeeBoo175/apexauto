using System.Data;
using Dealership.Application.Accounts;
using Dealership.Application.Sales;
using Dealership.Domain.Sales;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Vehicles;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Sales;

// The sales agent's own queue: holds in their branch waiting to be claimed, and the deals they are working or waiting on.
public sealed partial class DealStore
{
    // Member-initialised so the paging and counts translate to SQL.
    private sealed class QueueRow
    {
        public Guid ReservationId { get; init; }
        public Guid? DealId { get; init; }
        public string Plate { get; init; } = string.Empty;
        public string Make { get; init; } = string.Empty;
        public string Model { get; init; } = string.Empty;
        public string Trim { get; init; } = string.Empty;
        public int Year { get; init; }
        public string Customer { get; init; } = string.Empty;
        public DateTimeOffset HoldCreatedAt { get; init; }
        public DateTimeOffset ExpiresAt { get; init; }
        public DateTimeOffset? ClaimedAt { get; init; }
        public TimeSpan? Frozen { get; init; }
        public SalesOrderStatus? Status { get; init; }
        public bool Numbered { get; init; }
        public decimal? Balance { get; init; }
        public long? Version { get; init; }
        public DateTimeOffset SortAt { get; init; }
    }

    public async Task<DealResult> QueueAsync(Guid actorId, string view, int page, int pageSize,
        CancellationToken cancellationToken)
    {
        var actor = await ReadActorAsync(actorId, cancellationToken);
        if (actor is not { Role: AccountRoles.SalesAgent, BranchId: { } branchId })
        {
            return new(DealStatus.Forbidden);
        }

        // Expired holds are released before they are counted, as the manager reads do.
        var branchVehicles = database.Reservations.AsNoTracking().Where(item => item.BranchId == branchId)
            .Select(item => item.VehicleId);
        if (!await ReservationReadSnapshot.ReconcileDueAsync(database, branchVehicles, 10, cancellationToken))
        {
            return new(DealStatus.Unavailable);
        }

        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead,
            cancellationToken);
        var now = await DatabaseNowAsync(cancellationToken);
        var toClaim = OpenRows(branchId, now);
        var handling = DealRows(actor.Id, SalesOrderStatus.Draft);
        var waiting = DealRows(actor.Id, SalesOrderStatus.PendingManagerApproval);
        var counts = new SalesQueueCounts(await toClaim.CountAsync(cancellationToken),
            await handling.CountAsync(cancellationToken), await waiting.CountAsync(cancellationToken));
        var rows = view switch
        {
            "Handling" => handling,
            "Waiting" => waiting,
            _ => toClaim
        };
        var total = view switch
        {
            "Handling" => counts.Handling,
            "Waiting" => counts.Waiting,
            _ => counts.ToClaim
        };
        // Soonest expiry first for open holds; the longest-waiting claim or submission first elsewhere.
        var items = await rows.OrderBy(row => row.SortAt).ThenBy(row => row.ReservationId)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        var summaries = items.Select(row => new SalesQueueItem(row.ReservationId, row.DealId, row.Plate,
            $"{row.Year} {row.Make} {row.Model} {row.Trim}".Trim(), row.Customer, row.HoldCreatedAt,
            row.ClaimedAt is null ? row.ExpiresAt : null, row.Frozen is { } frozen ? (long)frozen.TotalSeconds : null,
            row.ClaimedAt, row.Status?.ToString(), row.Status is { } status ? DealNextStep.For(status, row.Numbered) : null,
            row.Balance, row.Version)).ToList();
        return new(DealStatus.Succeeded, Queue: new SalesQueuePage(summaries, total, page, pageSize, counts));
    }

    // Open holds in the branch, unclaimed and not yet past their deadline.
    private IQueryable<QueueRow> OpenRows(Guid branchId, DateTimeOffset now) =>
        from hold in database.Reservations.AsNoTracking()
        join vehicle in database.Vehicles.AsNoTracking() on hold.VehicleId equals vehicle.Id
        where hold.IsActive && hold.ClaimedById == null && hold.BranchId == branchId && hold.ExpiresAt > now
        select new QueueRow
        {
            ReservationId = hold.Id,
            Plate = vehicle.RegistrationPlate,
            Make = vehicle.Make,
            Model = vehicle.Model,
            Trim = vehicle.Trim,
            Year = vehicle.ManufactureYear,
            Customer = hold.Buyer.LegalName,
            HoldCreatedAt = hold.CreatedAt,
            ExpiresAt = hold.ExpiresAt,
            SortAt = hold.ExpiresAt
        };

    // The agent's own deals in one status, each with the claimed hold it came from.
    private IQueryable<QueueRow> DealRows(Guid agentId, SalesOrderStatus status) =>
        from order in database.SalesOrders.AsNoTracking()
        join hold in database.Reservations.AsNoTracking() on order.ReservationId equals hold.Id
        join vehicle in database.Vehicles.AsNoTracking() on order.VehicleId equals vehicle.Id
        where order.SalesAgentId == agentId && order.Status == status && hold.IsActive
        select new QueueRow
        {
            ReservationId = hold.Id,
            DealId = order.Id,
            Plate = vehicle.RegistrationPlate,
            Make = vehicle.Make,
            Model = vehicle.Model,
            Trim = vehicle.Trim,
            Year = vehicle.ManufactureYear,
            Customer = hold.Buyer.LegalName,
            HoldCreatedAt = hold.CreatedAt,
            ExpiresAt = hold.ExpiresAt,
            ClaimedAt = hold.ClaimedAt,
            Frozen = hold.FrozenRemaining,
            Status = order.Status,
            Numbered = order.OrderNumber != null,
            Balance = order.BalancePayable,
            Version = order.Version,
            SortAt = status == SalesOrderStatus.Draft ? hold.ClaimedAt ?? order.CreatedAtUtc
                : order.SubmittedAtUtc ?? order.UpdatedAtUtc
        };
}

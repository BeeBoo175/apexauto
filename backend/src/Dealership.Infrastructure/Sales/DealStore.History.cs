using System.Data;
using Dealership.Application.Accounts;
using Dealership.Application.Sales;
using Dealership.Domain.Sales;
using Dealership.Domain.Vehicles;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Sales;

// The retained receipts of a hold and the deal built on it, newest first, read from one database snapshot.
public sealed partial class DealStore
{
    private const string HoldSource = "Hold";
    private const string DealSource = "Deal";
    private const string RefundSource = "Refund";

    private sealed record HistoryRow(Guid Id, string Source, string Kind, DateTimeOffset At, Guid? ActorId,
        Guid? AgentId, Guid? PreviousAgentId, string? Reason, string? From, string? To,
        IReadOnlyList<DealChange>? Changes);

    public async Task<DealResult> HistoryAsync(Guid actorId, Guid id, bool ofDeal, int page, int pageSize,
        CancellationToken cancellationToken)
    {
        var actor = await ReadActorAsync(actorId, cancellationToken);
        if (actor is null)
        {
            return new(DealStatus.Forbidden);
        }

        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead,
            cancellationToken);
        Guid? reservationId;
        Guid? dealId = null;
        if (ofDeal)
        {
            var order = await database.SalesOrders.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id,
                cancellationToken);
            if (order is null || !CanSee(actor, order))
            {
                return new(DealStatus.NotFound);
            }

            reservationId = order.ReservationId;
            dealId = order.Id;
        }
        else
        {
            var hold = await database.Reservations.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id,
                cancellationToken);
            if (hold is null || !await CanSeeHoldAsync(actor, hold, cancellationToken))
            {
                return new(DealStatus.NotFound);
            }

            reservationId = hold.Id;
        }

        var rows = await ReadRowsAsync(reservationId, dealId, cancellationToken);
        var ordered = rows.OrderByDescending(row => row.At).ThenBy(row => row.Source).ThenByDescending(row => row.Id)
            .ToList();
        var slice = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        var ids = slice.SelectMany(row => new[] { row.ActorId, row.AgentId, row.PreviousAgentId })
            .Where(value => value is not null).Select(value => value!.Value).Distinct().ToArray();
        var emails = await database.Users.AsNoTracking().Where(item => ids.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => item.Email ?? string.Empty, cancellationToken);
        string? Email(Guid? value) => value is null ? null : emails.GetValueOrDefault(value.Value, string.Empty);
        var items = slice.Select(row => new HoldHistoryItem(row.Id, row.Source, row.Kind, row.At, row.ActorId,
            Email(row.ActorId) ?? "System", Email(row.AgentId), Email(row.PreviousAgentId), row.Reason, row.From,
            row.To, row.Changes ?? [])).ToList();
        await transaction.CommitAsync(cancellationToken);
        return new(DealStatus.Succeeded, History: new HoldHistoryPage(items, ordered.Count, page, pageSize));
    }

    // An agent sees the hold they handle or have handled a deal on; managers see their branch, the General Manager all.
    private async Task<bool> CanSeeHoldAsync(Actor actor, Reservation hold, CancellationToken cancellationToken) =>
        actor.Role switch
        {
            AccountRoles.SalesAgent => hold.BranchId == actor.BranchId && (hold.ClaimedById == actor.Id ||
                await database.SalesOrders.AnyAsync(item => item.ReservationId == hold.Id &&
                    item.SalesAgentId == actor.Id, cancellationToken)),
            AccountRoles.BranchManager => hold.BranchId == actor.BranchId,
            _ => true
        };

    private async Task<List<HistoryRow>> ReadRowsAsync(Guid? reservationId, Guid? dealId,
        CancellationToken cancellationToken)
    {
        var rows = new List<HistoryRow>();
        // Deal creation and reassignment duplicate the hold's claim and reassignment receipts, so the hold's are shown.
        var dealEvents = reservationId is { } held
            ? database.SalesOrderEvents.AsNoTracking().Where(item =>
                database.SalesOrders.Any(order => order.Id == item.SalesOrderId && order.ReservationId == held) &&
                item.Kind != SalesOrderEventKind.Created && item.Kind != SalesOrderEventKind.Reassigned)
            : database.SalesOrderEvents.AsNoTracking().Where(item => item.SalesOrderId == dealId);
        rows.AddRange((await dealEvents.ToListAsync(cancellationToken)).Select(item => new HistoryRow(item.Id,
            DealSource, item.Kind.ToString(), item.RecordedAtUtc, item.ActorId, null, null, item.Reason,
            item.FromStatus?.ToString(), item.ToStatus.ToString(), item.Changes)));
        if (reservationId is not { } id)
        {
            return rows;
        }

        var holdEvents = await database.ReservationEvents.AsNoTracking().Where(item => item.ReservationId == id)
            .ToListAsync(cancellationToken);
        rows.AddRange(holdEvents.Select(item => new HistoryRow(item.Id, HoldSource, item.Kind.ToString(),
            item.RecordedAtUtc, item.ActorId, item.AgentId, item.PreviousAgentId, null, null, null, null)));
        var releases = await database.ReservationReleases.AsNoTracking().Where(item => item.ReservationId == id)
            .ToListAsync(cancellationToken);
        rows.AddRange(releases.Select(item => new HistoryRow(item.Id, RefundSource, item.Kind.ToString(),
            item.RecordedAt, item.ActorId, null, null, item.Reason, null, null, null)));
        return rows;
    }
}

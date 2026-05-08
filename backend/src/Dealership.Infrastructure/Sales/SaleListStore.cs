using System.Data;
using Dealership.Application.Accounts;
using Dealership.Application.Sales;
using Dealership.Domain.Sales;
using Dealership.Infrastructure.Persistence;
using Dealership.Infrastructure.Vehicles;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Sales;

// The agent's queue and the managers' table over one sale per reservation.
public sealed class SaleListStore(DealershipDbContext database) : ISaleListStore
{
    // Each view's own order: what needs attention soonest, or what has waited longest, comes first.
    private static readonly IReadOnlyDictionary<string, (string Sort, bool Descending)> DefaultOrders =
        new Dictionary<string, (string, bool)>
        {
            ["ToClaim"] = ("due", false),
            ["Open"] = ("due", false),
            ["Handling"] = ("claimed", false),
            ["BeingHandled"] = ("claimed", false),
            ["Waiting"] = ("submitted", false),
            ["NeedsApproval"] = ("submitted", false),
            ["Approved"] = ("decided", false),
            ["History"] = ("closed", true)
        };

    public async Task<SaleReadResult<SaleQueuePage>> QueueAsync(Guid actorId, string view, string? search, int page,
        int pageSize, CancellationToken cancellationToken)
    {
        var actor = await SaleStaffLocks.ReadAsync(database, actorId, cancellationToken);
        if (actor is not { IsActive: true, Role: AccountRoles.SalesAgent, BranchId: { } branchId } ||
            !await database.Branches.AnyAsync(branch => branch.Id == branchId && branch.IsActive, cancellationToken))
        {
            return new(SaleStatus.Forbidden);
        }

        if (!await ReconcileAsync(database.SalesOrders.Where(item => item.BranchId == branchId), cancellationToken))
        {
            return new(SaleStatus.Unavailable);
        }

        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead,
            cancellationToken);
        var sales = database.SalesOrders.AsNoTracking();
        var views = new Dictionary<string, IQueryable<SaleRow>>
        {
            ["ToClaim"] = Rows(sales.Where(item => item.BranchId == branchId && item.Status == SalesOrderStatus.Open),
                search),
            ["Handling"] = Rows(Own(sales, actorId, SalesOrderStatus.Handling), search),
            ["Waiting"] = Rows(Own(sales, actorId, SalesOrderStatus.PendingApproval), search),
            ["Approved"] = Rows(Own(sales, actorId, SalesOrderStatus.Approved), search)
        };
        var counts = new SaleQueueCounts(await views["ToClaim"].CountAsync(cancellationToken),
            await views["Handling"].CountAsync(cancellationToken), await views["Waiting"].CountAsync(cancellationToken),
            await views["Approved"].CountAsync(cancellationToken));
        // A search spans every tab; each match carries its status so the queue can show where it sits.
        var rows = search is null ? views[view] : views.Values.Aggregate((all, next) => all.Concat(next));
        var total = search is null ? CountOf(counts, view) : counts.ToClaim + counts.Handling + counts.Waiting +
            counts.Approved;
        var (sort, descending) = DefaultOrders[view];
        var items = await SaleRows.Order(rows, search is null ? sort : "due", descending)
            .ReadPageAsync(page, pageSize, total, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(SaleStatus.Succeeded,
            new SaleQueuePage(items.Select(SaleRows.Summary).ToList(), total, page, pageSize, counts));
    }

    public async Task<SaleReadResult<SaleTablePage>> TableAsync(Guid actorId, SaleTableQuery query,
        CancellationToken cancellationToken)
    {
        var actor = await SaleStaffLocks.ReadAsync(database, actorId, cancellationToken);
        var group = actor is { IsActive: true, Role: AccountRoles.GeneralManager };
        var branch = actor is { IsActive: true, Role: AccountRoles.BranchManager, BranchId: { } branchId } &&
            await database.Branches.AnyAsync(item => item.Id == branchId && item.IsActive, cancellationToken);
        if (!group && !branch)
        {
            return new(SaleStatus.Forbidden);
        }

        var scoped = group ? database.SalesOrders : database.SalesOrders.Where(item => item.BranchId == actor!.BranchId);
        if (!await ReconcileAsync(scoped, cancellationToken))
        {
            return new(SaleStatus.Unavailable);
        }

        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead,
            cancellationToken);
        var sales = scoped.AsNoTracking();
        // Tab totals describe the role's scope and ignore search and column filters, like every staff table.
        var counts = new SaleTableCounts(
            await sales.CountAsync(item => item.Status == SalesOrderStatus.PendingApproval, cancellationToken),
            await sales.CountAsync(item => item.Status == SalesOrderStatus.Open, cancellationToken),
            await sales.CountAsync(item => item.Status == SalesOrderStatus.Handling, cancellationToken),
            await sales.CountAsync(item => item.Status == SalesOrderStatus.Approved, cancellationToken),
            await sales.CountAsync(item => item.Status == SalesOrderStatus.Expired ||
                item.Status == SalesOrderStatus.Cancelled, cancellationToken));
        var view = query.View ?? FirstWithWork(counts);
        var rows = Filter(SaleRows.Project(database, InView(sales, view)), query, group);
        var total = await rows.CountAsync(cancellationToken);
        var (sort, descending) = query.Sort is null ? DefaultOrders[view] : (query.Sort, query.Descending);
        var items = await SaleRows.Order(rows, sort, descending).ReadPageAsync(query.Page, query.PageSize, total,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(SaleStatus.Succeeded, new SaleTablePage(items.Select(SaleRows.Summary).ToList(), total, query.Page,
            query.PageSize, counts, view));
    }

    private IQueryable<SaleRow> Rows(IQueryable<SalesOrder> sales, string? search)
    {
        var rows = SaleRows.Project(database, sales);
        return search is null ? rows : SaleRows.Search(rows, search, false);
    }

    private static IQueryable<SalesOrder> Own(IQueryable<SalesOrder> sales, Guid agentId, SalesOrderStatus status) =>
        sales.Where(item => item.AgentId == agentId && item.Status == status);

    private static int CountOf(SaleQueueCounts counts, string view) => view switch
    {
        "Handling" => counts.Handling,
        "Waiting" => counts.Waiting,
        "Approved" => counts.Approved,
        _ => counts.ToClaim
    };

    // The manager table opens on the first tab with work, in the order the tabs are shown.
    private static string FirstWithWork(SaleTableCounts counts)
    {
        if (counts.NeedsApproval > 0)
        {
            return "NeedsApproval";
        }

        if (counts.Open > 0)
        {
            return "Open";
        }

        if (counts.BeingHandled > 0)
        {
            return "BeingHandled";
        }

        if (counts.Approved > 0)
        {
            return "Approved";
        }

        return "NeedsApproval";
    }

    private static IQueryable<SalesOrder> InView(IQueryable<SalesOrder> sales, string view) => view switch
    {
        "Open" => sales.Where(item => item.Status == SalesOrderStatus.Open),
        "BeingHandled" => sales.Where(item => item.Status == SalesOrderStatus.Handling),
        "Approved" => sales.Where(item => item.Status == SalesOrderStatus.Approved),
        "History" => sales.Where(item => item.Status == SalesOrderStatus.Expired ||
            item.Status == SalesOrderStatus.Cancelled),
        _ => sales.Where(item => item.Status == SalesOrderStatus.PendingApproval)
    };

    private static IQueryable<SaleRow> Filter(IQueryable<SaleRow> rows, SaleTableQuery query, bool group)
    {
        if (query.Agent is { } agent)
        {
            var pattern = LiteralSearch.Pattern(agent);
            rows = rows.Where(row => row.Agent != null && EF.Functions.ILike(row.Agent, pattern, "!"));
        }

        if (group && query.Branch is { } branch)
        {
            var pattern = LiteralSearch.Pattern(branch);
            rows = rows.Where(row => EF.Functions.ILike(row.Branch, pattern, "!"));
        }

        if (query.Outcome is { } outcome)
        {
            var status = Enum.Parse<SalesOrderStatus>(outcome);
            rows = rows.Where(row => row.Status == status);
        }

        if (query.MinimumDiscount is { } minimumDiscount)
        {
            rows = rows.Where(row => row.NegotiatedDiscount >= minimumDiscount);
        }

        if (query.MaximumDiscount is { } maximumDiscount)
        {
            rows = rows.Where(row => row.NegotiatedDiscount <= maximumDiscount);
        }

        if (query.MinimumBalance is { } minimumBalance)
        {
            rows = rows.Where(row => row.BalancePayable >= minimumBalance);
        }

        if (query.MaximumBalance is { } maximumBalance)
        {
            rows = rows.Where(row => row.BalancePayable <= maximumBalance);
        }

        if (query.Submitted is { From: { } submittedFrom, Until: { } submittedUntil })
        {
            rows = rows.Where(row => row.SubmittedAt >= submittedFrom && row.SubmittedAt < submittedUntil);
        }

        if (query.Closed is { From: { } closedFrom, Until: { } closedUntil })
        {
            rows = rows.Where(row => row.ClosedAt >= closedFrom && row.ClosedAt < closedUntil);
        }

        return query.Search is null ? rows : SaleRows.Search(rows, query.Search, group);
    }

    // Open sales past their deadline are expired, with their refunds, before anyone counts or reads them.
    private Task<bool> ReconcileAsync(IQueryable<SalesOrder> scope, CancellationToken cancellationToken) =>
        ReservationReadSnapshot.ReconcileDueAsync(database, scope.Select(item => item.VehicleId), 10,
            cancellationToken);
}

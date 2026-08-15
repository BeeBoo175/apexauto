using System.Collections.Frozen;
using System.Data;
using Dealership.Application.Accounts;
using Dealership.Application.Sales;
using Dealership.Domain.Sales;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Persistence;
using Dealership.Infrastructure.Vehicles;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Sales;

// The agent's queue and the managers' table over one sale per reservation.
public sealed class SaleListStore(DealershipDbContext database) : ISaleListStore
{
    // Each view's own order: what needs attention soonest, or what has waited longest, comes first.
    private static readonly FrozenDictionary<string, (string Sort, bool Descending)> DefaultOrders =
        new Dictionary<string, (string Sort, bool Descending)>
        {
            [SaleViews.ToClaim] = ("due", false),
            [SaleViews.Open] = ("due", false),
            [SaleViews.Handling] = ("claimed", false),
            [SaleViews.BeingHandled] = ("claimed", false),
            [SaleViews.Waiting] = ("submitted", false),
            [SaleViews.NeedsApproval] = ("submitted", false),
            [SaleViews.Approved] = ("decided", false),
            [SaleViews.Settling] = ("decided", false),
            [SaleViews.AwaitingHandover] = ("settled", false),
            [SaleViews.Completed] = ("closed", true),
            [SaleViews.Delivered] = ("closed", true),
            [SaleViews.History] = ("closed", true)
        }.ToFrozenDictionary();

    public async Task<SaleReadResult<SaleQueuePage>> QueueAsync(Guid actorId, string view, string? search, int page,
        int pageSize, CancellationToken cancellationToken)
    {
        var actor = await SaleStaffLocks.FindAsync(database, actorId, cancellationToken);
        if (actor is not { IsActive: true, Role: AccountRoles.SalesAgent, BranchId: { } branchId } ||
            !await SaleStaffLocks.BranchActiveAsync(database, actor, cancellationToken))
        {
            return new(SaleResultStatus.Forbidden);
        }

        if (!await ReconcileAsync(database.SalesOrders.Where(item => item.BranchId == branchId), cancellationToken))
        {
            return new(SaleResultStatus.Unavailable);
        }

        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead,
            cancellationToken);
        var sales = database.SalesOrders.AsNoTracking().Where(item => item.BranchId == branchId);
        var tabs = new Dictionary<string, IQueryable<SalesOrder>>
        {
            [SaleViews.ToClaim] = sales.Where(item => item.Status == SalesOrderStatus.Open),
            [SaleViews.Handling] = Own(sales, actorId, SalesOrderStatus.Handling),
            [SaleViews.Waiting] = Own(sales, actorId, SalesOrderStatus.PendingApproval),
            [SaleViews.Settling] = sales.Where(item => item.AgentId == actorId &&
                (item.Status == SalesOrderStatus.Approved || item.Status == SalesOrderStatus.Settled)),
            [SaleViews.Completed] = Own(sales, actorId, SalesOrderStatus.Delivered)
        };
        var views = tabs.ToDictionary(item => item.Key, item => Rows(item.Value, search));
        // Without a search the counts need no customer, car or agent details, so they run on the sales alone.
        var counts = new SaleQueueCounts(await CountAsync(SaleViews.ToClaim), await CountAsync(SaleViews.Handling),
            await CountAsync(SaleViews.Waiting), await CountAsync(SaleViews.Settling), await CountAsync(SaleViews.Completed));
        // A search spans every tab; each match carries its status so the queue can show where it sits.
        var rows = search is null ? views[view] : views.Values.Aggregate((all, next) => all.Concat(next));
        var total = search is null ? CountOf(counts, view) : counts.ToClaim + counts.Handling + counts.Waiting +
            counts.Settling + counts.Completed;
        var (sort, descending) = DefaultOrders[view];
        var items = await SaleRows.Order(rows, search is null ? sort : "due", descending)
            .ReadPageAsync(page, pageSize, total, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(SaleResultStatus.Succeeded,
            new SaleQueuePage([.. items.Select(SaleRows.Summary)], total, page, pageSize, counts));

        Task<int> CountAsync(string tab) => search is null
            ? tabs[tab].CountAsync(cancellationToken)
            : views[tab].CountAsync(cancellationToken);
    }

    public async Task<SaleReadResult<SaleTablePage>> TableAsync(Guid actorId, SaleTableQuery query,
        CancellationToken cancellationToken)
    {
        var actor = await SaleStaffLocks.FindAsync(database, actorId, cancellationToken);
        var group = actor is { IsActive: true, Role: AccountRoles.GeneralManager };
        var branch = actor is { IsActive: true, Role: AccountRoles.BranchManager, BranchId: not null } &&
            await SaleStaffLocks.BranchActiveAsync(database, actor, cancellationToken);
        if (!group && !branch)
        {
            return new(SaleResultStatus.Forbidden);
        }

        var scoped = group ? database.SalesOrders : database.SalesOrders.Where(item => item.BranchId == actor!.BranchId);
        if (!await ReconcileAsync(scoped, cancellationToken))
        {
            return new(SaleResultStatus.Unavailable);
        }

        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead,
            cancellationToken);
        var sales = scoped.AsNoTracking();
        var now = await database.DatabaseNowAsync(cancellationToken);
        var pending = PendingDeposits(now, group ? null : actor!.BranchId);
        // Tab totals describe the role's scope and ignore search and column filters, like every staff table.
        var counts = new SaleTableCounts(
            await sales.CountAsync(item => item.Status == SalesOrderStatus.PendingApproval, cancellationToken),
            await sales.CountAsync(item => item.Status == SalesOrderStatus.Open, cancellationToken),
            await sales.CountAsync(item => item.Status == SalesOrderStatus.Handling, cancellationToken),
            await sales.CountAsync(item => item.Status == SalesOrderStatus.Approved, cancellationToken),
            await sales.CountAsync(item => item.Status == SalesOrderStatus.Settled, cancellationToken),
            await sales.CountAsync(item => item.Status == SalesOrderStatus.Delivered, cancellationToken),
            await sales.CountAsync(item => item.Status == SalesOrderStatus.Expired ||
                item.Status == SalesOrderStatus.Cancelled, cancellationToken),
            await pending.CountAsync(cancellationToken));
        var view = query.View ?? FirstWithWork(counts);
        if (view == SaleViews.AwaitingDeposit)
        {
            var requests = await DepositRowsAsync(pending, query, counts.AwaitingDeposit, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(SaleResultStatus.Succeeded, new SaleTablePage([], counts.AwaitingDeposit, query.Page,
                query.PageSize, counts, view, requests));
        }

        var rows = Filter(SaleRows.Project(database, InView(sales, view)), query, group);
        rows = SaleRows.TimeLeftBetween(rows, query.MinimumHoursLeft, query.MaximumHoursLeft, now);
        var total = await rows.CountAsync(cancellationToken);
        var (sort, descending) = query.Sort is null ? DefaultOrders[view] : (query.Sort, query.Descending);
        if (sort == "timeLeft")
        {
            sort = view == SaleViews.Open ? "due" : "paused";
        }
        var items = await SaleRows.Order(rows, sort, descending).ReadPageAsync(query.Page, query.PageSize, total,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(SaleResultStatus.Succeeded, new SaleTablePage([.. items.Select(SaleRows.Summary)], total, query.Page,
            query.PageSize, counts, view));
    }

    // Requests still waiting for their customer: not confirmed, cancelled or lapsed, within the role's branch.
    private IQueryable<ReservationDepositRequest> PendingDeposits(DateTimeOffset now, Guid? branchId)
    {
        return database.ReservationDepositRequests.AsNoTracking().Where(request => request.ConfirmedAt == null &&
            request.CancelledAt == null && request.ExpiresAt > now && (branchId == null || request.BranchId == branchId));
    }

    private async Task<IReadOnlyList<DepositRequestRow>> DepositRowsAsync(IQueryable<ReservationDepositRequest> pending,
        SaleTableQuery query, int total, CancellationToken cancellationToken)
    {
        // Ordering and search read the joined entities, and the rows are shaped only after the page is read.
        var rows = from request in pending
                   join vehicle in database.Vehicles.AsNoTracking() on request.VehicleId equals vehicle.Id
                   join branch in database.Branches.AsNoTracking() on request.BranchId equals branch.Id
                   join profile in database.CustomerProfiles.AsNoTracking() on request.CustomerId equals profile.UserId
                   join staff in database.Users.AsNoTracking() on request.BookedByStaffId equals staff.Id
                   select new { request, vehicle, branch, profile, staff };
        if (query.Search is { } search)
        {
            var pattern = LiteralSearch.Pattern(search);
            rows = rows.Where(row => EF.Functions.ILike(row.profile.LegalName, pattern, "!") ||
                EF.Functions.ILike(row.vehicle.RegistrationPlate, pattern, "!") ||
                EF.Functions.ILike(row.vehicle.Make + " " + row.vehicle.Model, pattern, "!"));
        }

        var count = query.Search is null ? total : await rows.CountAsync(cancellationToken);
        var ordered = (query.Sort, query.Descending) switch
        {
            ("customer", false) => rows.OrderBy(row => row.profile.LegalName).ThenBy(row => row.request.Id),
            ("customer", true) => rows.OrderByDescending(row => row.profile.LegalName).ThenBy(row => row.request.Id),
            ("vehicle", false) => rows.OrderBy(row => row.vehicle.Make).ThenBy(row => row.vehicle.Model).ThenBy(row => row.request.Id),
            ("vehicle", true) => rows.OrderByDescending(row => row.vehicle.Make).ThenByDescending(row => row.vehicle.Model)
                .ThenBy(row => row.request.Id),
            ("branch", false) => rows.OrderBy(row => row.branch.Name).ThenBy(row => row.request.Id),
            ("branch", true) => rows.OrderByDescending(row => row.branch.Name).ThenBy(row => row.request.Id),
            (_, true) => rows.OrderByDescending(row => row.request.ExpiresAt).ThenBy(row => row.request.Id),
            _ => rows.OrderBy(row => row.request.ExpiresAt).ThenBy(row => row.request.Id)
        };
        var page = await ordered.ReadPageAsync(query.Page, query.PageSize, count, cancellationToken);
        return [.. page.Select(row => new DepositRequestRow(row.request.Id, row.vehicle.Id, row.vehicle.RegistrationPlate,
            row.vehicle.ManufactureYear, row.vehicle.Make, row.vehicle.Model, row.vehicle.Trim, row.request.CustomerId,
            row.profile.LegalName, row.branch.Name, row.staff.Email!, row.request.ExpiresAt))];
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
        SaleViews.Handling => counts.Handling,
        SaleViews.Waiting => counts.Waiting,
        SaleViews.Settling => counts.Settling,
        SaleViews.Completed => counts.Completed,
        _ => counts.ToClaim
    };

    // The manager table opens on the first tab with work, in the order the tabs are shown.
    private static string FirstWithWork(SaleTableCounts counts)
    {
        if (counts.NeedsApproval > 0)
        {
            return SaleViews.NeedsApproval;
        }

        if (counts.Open > 0)
        {
            return SaleViews.Open;
        }

        if (counts.BeingHandled > 0)
        {
            return SaleViews.BeingHandled;
        }

        if (counts.Approved > 0)
        {
            return SaleViews.Approved;
        }

        if (counts.AwaitingHandover > 0)
        {
            return SaleViews.AwaitingHandover;
        }

        if (counts.Delivered > 0)
        {
            return SaleViews.Delivered;
        }

        return SaleViews.NeedsApproval;
    }

    private static IQueryable<SalesOrder> InView(IQueryable<SalesOrder> sales, string view) => view switch
    {
        SaleViews.Open => sales.Where(item => item.Status == SalesOrderStatus.Open),
        SaleViews.BeingHandled => sales.Where(item => item.Status == SalesOrderStatus.Handling),
        SaleViews.Approved => sales.Where(item => item.Status == SalesOrderStatus.Approved),
        SaleViews.AwaitingHandover => sales.Where(item => item.Status == SalesOrderStatus.Settled),
        SaleViews.Delivered => sales.Where(item => item.Status == SalesOrderStatus.Delivered),
        SaleViews.History => sales.Where(item => item.Status == SalesOrderStatus.Expired ||
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

        if (query.Registration is { } registration)
        {
            var pattern = LiteralSearch.Pattern(registration);
            rows = rows.Where(row => EF.Functions.ILike(row.Plate, pattern, "!"));
        }

        if (query.Vehicle is { } vehicle)
        {
            var pattern = LiteralSearch.Pattern(vehicle);
            rows = rows.Where(row =>
                EF.Functions.ILike(row.Year + " " + row.Make + " " + row.Model + " " + row.Trim, pattern, "!"));
        }

        if (query.Customer is { } customer)
        {
            var pattern = LiteralSearch.Pattern(customer);
            rows = rows.Where(row => EF.Functions.ILike(row.Customer, pattern, "!"));
        }

        // The reference is the order number once submitted, the reservation number before.
        if (query.Reference is { } reference)
        {
            var pattern = LiteralSearch.Pattern(reference);
            rows = rows.Where(row => EF.Functions.ILike(row.OrderNumber ?? row.ReservationNumber, pattern, "!"));
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

        if (query.Updated is { From: { } updatedFrom, Until: { } updatedUntil })
        {
            rows = rows.Where(row => row.UpdatedAt >= updatedFrom && row.UpdatedAt < updatedUntil);
        }

        return query.Search is null ? rows : SaleRows.Search(rows, query.Search, group);
    }

    // Open sales past their deadline are expired, with their refunds, before anyone counts or reads them.
    private Task<bool> ReconcileAsync(IQueryable<SalesOrder> scope, CancellationToken cancellationToken) =>
        ReservationReadSnapshot.ReconcileDueAsync(database, scope.Select(item => item.VehicleId), 10,
            cancellationToken);
}

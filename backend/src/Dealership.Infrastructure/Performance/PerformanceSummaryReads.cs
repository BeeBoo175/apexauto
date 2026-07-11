using System.Data;
using Dealership.Application.Accounts;
using Dealership.Application.Performance;
using Dealership.Domain.Sales;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Performance;

// Manager operations reporting: every section is a bounded aggregate over retained event rows, attributed to the
// branch recorded at the event, never to a person's current branch.
public sealed class PerformanceSummaryReads(DealershipDbContext database) : IPerformanceSummaryReads
{
    public async Task<PerformanceReadResult<PerformanceHeadline>> HeadlineAsync(Guid actorId, PerformanceQuery query,
        CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead,
            cancellationToken);
        var scope = await PerformanceScopes.ResolveAsync(database, actorId, query, cancellationToken);
        if (scope is null)
        {
            return new(PerformanceReadStatus.Forbidden);
        }

        var now = await database.DatabaseNowAsync(cancellationToken);
        var previousFrom = query.From - query.Length;
        var current = await PerformanceFigures.TotalsAsync(database, scope.BranchId, query.From, query.Until, now, cancellationToken);
        var previous = await PerformanceFigures.TotalsAsync(database, scope.BranchId, previousFrom, query.From, now, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(PerformanceReadStatus.Succeeded, new PerformanceHeadline(scope.Actor, current, previous));
    }

    public async Task<PerformanceReadResult<PerformancePipeline>> PipelineAsync(Guid actorId, PerformanceQuery query,
        CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead,
            cancellationToken);
        var scope = await PerformanceScopes.ResolveAsync(database, actorId, query, cancellationToken);
        if (scope is null)
        {
            return new(PerformanceReadStatus.Forbidden);
        }

        var now = await database.DatabaseNowAsync(cancellationToken);
        var open = PerformanceScopes.SalesInScope(database, scope.BranchId).Where(order =>
            order.Status == SalesOrderStatus.Handling || order.Status == SalesOrderStatus.PendingApproval ||
            order.Status == SalesOrderStatus.Approved || order.Status == SalesOrderStatus.Settled ||
            order.Status == SalesOrderStatus.Open && (order.DueAt == null || order.DueAt > now));
        var rows = await open.GroupBy(order => order.Status)
            .Select(group => new
            {
                Status = group.Key,
                Count = group.Count(),
                Total = group.Sum(order => order.BalancePayable + order.DepositCredit + order.TradeInAllowance)
            })
            .ToListAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        var groups = PerformancePipelineGroups.Ordered.Select(name =>
        {
            var row = rows.SingleOrDefault(item => GroupOf(item.Status) == name);
            return new PerformancePipelineGroup(name, row?.Count ?? 0, row?.Total ?? 0m);
        }).ToArray();
        return new(PerformanceReadStatus.Succeeded, new PerformancePipeline(scope.Actor, groups));
    }

    private static string GroupOf(SalesOrderStatus status)
    {
        return status switch
        {
            SalesOrderStatus.Open => PerformancePipelineGroups.Unclaimed,
            SalesOrderStatus.Handling => PerformancePipelineGroups.Handling,
            SalesOrderStatus.PendingApproval => PerformancePipelineGroups.NeedsApproval,
            SalesOrderStatus.Approved => PerformancePipelineGroups.Approved,
            _ => PerformancePipelineGroups.SettledUndelivered
        };
    }

    // GeneralManager only: the headline measures once per active branch, reusing the headline totals so the two views
    // can never disagree.
    public async Task<PerformanceReadResult<PerformanceBranchComparison>> BranchComparisonAsync(Guid actorId,
        PerformanceQuery query, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead,
            cancellationToken);
        var scope = await PerformanceScopes.ResolveAsync(database, actorId, query, cancellationToken);
        if (scope is null || scope.Actor.Role != AccountRoles.GeneralManager)
        {
            return new(PerformanceReadStatus.Forbidden);
        }

        var now = await database.DatabaseNowAsync(cancellationToken);
        var branches = await database.Branches.AsNoTracking().Where(branch => branch.IsActive)
            .OrderBy(branch => branch.Name).ThenBy(branch => branch.Id)
            .Select(branch => new { branch.Id, branch.Name })
            .ToListAsync(cancellationToken);
        var previousFrom = query.From - query.Length;
        var rows = new List<PerformanceBranchRow>();
        foreach (var branch in branches)
        {
            var current = await PerformanceFigures.TotalsAsync(database, branch.Id, query.From, query.Until, now, cancellationToken);
            var previous = await PerformanceFigures.TotalsAsync(database, branch.Id, previousFrom, query.From, now, cancellationToken);
            var weeks = await PerformanceFigures.WeeklyFlowAsync(database, branch.Id, query, cancellationToken);
            rows.Add(new(branch.Id, branch.Name, current, previous, weeks));
        }

        await transaction.CommitAsync(cancellationToken);
        return new(PerformanceReadStatus.Succeeded, new PerformanceBranchComparison(scope.Actor, rows));
    }
}

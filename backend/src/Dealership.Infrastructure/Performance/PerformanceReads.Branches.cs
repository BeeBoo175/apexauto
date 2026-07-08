using System.Data;
using Dealership.Application.Accounts;
using Dealership.Application.Performance;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Performance;

public sealed partial class PerformanceReads
{
    // GeneralManager only: the headline measures once per active branch, reusing the headline totals so the two views
    // can never disagree.
    public async Task<PerformanceReadResult<PerformanceBranchComparison>> BranchComparisonAsync(Guid actorId,
        PerformanceQuery query, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead,
            cancellationToken);
        var scope = await ResolveScopeAsync(actorId, query, cancellationToken);
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
            var current = await TotalsAsync(branch.Id, query.From, query.Until, now, cancellationToken);
            var previous = await TotalsAsync(branch.Id, previousFrom, query.From, now, cancellationToken);
            var weeks = await WeeklyFlowAsync(branch.Id, query, cancellationToken);
            rows.Add(new(branch.Id, branch.Name, current, previous, weeks));
        }

        await transaction.CommitAsync(cancellationToken);
        return new(PerformanceReadStatus.Succeeded, new PerformanceBranchComparison(scope.Actor, rows));
    }
}

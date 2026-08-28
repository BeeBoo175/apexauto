using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.WorkCalendar;

internal static class BranchShortCodes
{
    // One small read for the branches a calendar page shows, so the work rows need not each join the branch.
    public static async Task<Dictionary<Guid, string>> ReadAsync(DealershipDbContext database,
        IEnumerable<Guid> branchIds, CancellationToken cancellationToken)
    {
        var ids = branchIds.Distinct().ToArray();
        return await database.Branches.AsNoTracking().Where(branch => ids.Contains(branch.Id))
            .ToDictionaryAsync(branch => branch.Id, branch => branch.ShortCode, cancellationToken);
    }
}
using Dealership.Application.Accounts;
using Dealership.Application.Performance;
using Dealership.Domain.Sales;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Performance;

internal sealed record PerformanceScope(CurrentActor Actor, Guid? BranchId);

internal static class PerformanceScopes
{
    // The branch managers read is their own branch; a GeneralManager reads all branches or one chosen branch.
    internal static async Task<PerformanceScope?> ResolveAsync(DealershipDbContext database, Guid actorId,
        PerformanceQuery query, CancellationToken cancellationToken)
    {
        var actor = await database.Users.AsNoTracking()
            .Where(user => user.Id == actorId && user.IsActive &&
                (user.Role == AccountRoles.BranchManager || user.Role == AccountRoles.GeneralManager))
            .Select(ActorRows.Current)
            .SingleOrDefaultAsync(cancellationToken);
        if (actor is null)
        {
            return null;
        }

        return actor.Role == AccountRoles.BranchManager
            ? new(actor, actor.BranchId)
            : new(actor, query.BranchId);
    }

    internal static IQueryable<SalesOrder> SalesInScope(DealershipDbContext database, Guid? branchId)
    {
        return database.SalesOrders.AsNoTracking().Where(order => branchId == null || order.BranchId == branchId);
    }
}

using Dealership.Application.Accounts;
using Dealership.Infrastructure.Identity;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Inspections;

internal static class InspectionQueueScope
{
    public static async Task<AccountUser?> ReadAsync(DealershipDbContext database, Guid actorId,
        Guid? selectedBranch, CancellationToken cancellationToken)
    {
        var actor = await database.Users.AsNoTracking().SingleOrDefaultAsync(item => item.Id == actorId, cancellationToken);
        if (actor is not { IsActive: true } ||
            actor.Role is not (AccountRoles.WorkshopTechnician or AccountRoles.BranchManager or AccountRoles.GeneralManager))
        {
            return null;
        }
        if (actor.Role == AccountRoles.GeneralManager)
        {
            return actor;
        }
        if (actor.BranchId is null || selectedBranch is not null && selectedBranch != actor.BranchId ||
            !await database.Branches.AnyAsync(item => item.Id == actor.BranchId && item.IsActive, cancellationToken))
        {
            return null;
        }

        return actor;
    }
}

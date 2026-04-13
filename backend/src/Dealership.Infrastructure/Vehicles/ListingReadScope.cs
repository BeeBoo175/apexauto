using Dealership.Application.Accounts;
using Dealership.Application.Vehicles;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Vehicles;

internal static class ListingReadScope
{
    public static async Task<ListingManager?> ReadActorAsync(DealershipDbContext database, Guid actorId,
        Guid? selectedBranch, CancellationToken cancellationToken)
    {
        var actor = await database.Users.AsNoTracking().Where(user => user.Id == actorId)
            .Select(user => new ListingManager(user.IsActive, user.Role, user.BranchId)).SingleOrDefaultAsync(cancellationToken);
        if (actor is not { IsActive: true } || actor.Role is not (AccountRoles.BranchManager or AccountRoles.GeneralManager))
        {
            return null;
        }

        if (actor.Role == AccountRoles.GeneralManager)
        {
            return actor;
        }

        if (actor.BranchId is null || selectedBranch is not null && selectedBranch != actor.BranchId ||
            !await database.Branches.AnyAsync(branch => branch.Id == actor.BranchId && branch.IsActive, cancellationToken))
        {
            return null;
        }

        return actor;
    }
}

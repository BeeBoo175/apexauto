using Dealership.Application.Accounts;
using Dealership.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Persistence;

internal static class StaffHandlerRows
{
    public static IQueryable<AccountUser> Eligible(DealershipDbContext database)
    {
        return database.Users.AsNoTracking().Where(user => user.IsActive &&
            (user.Role == AccountRoles.SalesAgent || user.Role == AccountRoles.BranchManager) &&
            database.Branches.Any(branch => branch.Id == user.BranchId && branch.IsActive));
    }

    public static Task<StaffHandler[]> ReadAsync(DealershipDbContext database, Guid? branchId,
        CancellationToken cancellationToken)
    {
        return Eligible(database).Where(user => user.BranchId == branchId)
            .OrderBy(user => user.Email).ThenBy(user => user.Id)
            .Select(user => new StaffHandler(user.Id, user.Email ?? string.Empty))
            .ToArrayAsync(cancellationToken);
    }
}

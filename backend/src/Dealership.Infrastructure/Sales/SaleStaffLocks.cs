using Dealership.Application.Sales;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Sales;

internal static class SaleStaffLocks
{
    // Shares the staff row so a role, branch or deactivation change waits for the command that relies on it.
    public static async Task<SaleStaff?> LockAsync(DealershipDbContext database, Guid staffId,
        CancellationToken cancellationToken)
    {
        var user = await database.Users.FromSqlInterpolated(
            $"SELECT * FROM \"AspNetUsers\" WHERE \"Id\" = {staffId} FOR SHARE").AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        return user is null ? null : new SaleStaff(user.Id, user.IsActive, user.Role, user.BranchId);
    }

    // Reads the staff row without locking it; commands that rely on the row use LockAsync.
    public static async Task<SaleStaff?> FindAsync(DealershipDbContext database, Guid staffId,
        CancellationToken cancellationToken)
    {
        var user = await database.Users.AsNoTracking().SingleOrDefaultAsync(item => item.Id == staffId,
            cancellationToken);
        return user is null ? null : new SaleStaff(user.Id, user.IsActive, user.Role, user.BranchId);
    }

    // Sales staff without a branch (administrators) have no branch to deactivate; the rest need an active one.
    public static async Task<bool> BranchActiveAsync(DealershipDbContext database, SaleStaff? actor,
        CancellationToken cancellationToken) =>
        actor?.BranchId is not { } branchId ||
        await database.Branches.AnyAsync(branch => branch.Id == branchId && branch.IsActive, cancellationToken);
}

using Dealership.Application.Accounts;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Intake;

internal sealed record StaffIntakeReadScope(Guid? BranchId, string Role)
{
    public static async Task<StaffIntakeReadScope?> ResolveAsync(DealershipDbContext database, Guid staffId,
        CancellationToken cancellationToken)
    {
        var staff = await database.Users.AsNoTracking().Where(user => user.Id == staffId)
            .Select(user => new { user.IsActive, user.Role, user.BranchId }).SingleOrDefaultAsync(cancellationToken);

        if (staff is not { IsActive: true } ||
            staff.Role is not (AccountRoles.SalesAgent or AccountRoles.BranchManager or AccountRoles.GeneralManager))
        {
            return null;
        }

        if (staff.Role == AccountRoles.GeneralManager)
        {
            return new StaffIntakeReadScope(BranchId: null, staff.Role);
        }

        if (staff.BranchId is null ||
            !await database.Branches.AnyAsync(branch => branch.Id == staff.BranchId && branch.IsActive, cancellationToken))
        {
            return null;
        }

        return new StaffIntakeReadScope(staff.BranchId, staff.Role);
    }
}

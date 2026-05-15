using Dealership.Application.Accounts;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Identity;

// Administration is system-wide and unbranched; commercial approvals do not flow through this scope.
internal static class AdministratorScope
{
    public static Task<bool> IsAdministratorAsync(DealershipDbContext database, Guid actorId,
        CancellationToken cancellationToken) =>
        database.Users.AsNoTracking().AnyAsync(user => user.Id == actorId && user.IsActive &&
            user.Role == AccountRoles.Administrator, cancellationToken);

    // Share-locks the actor row so deactivation or a role change cannot race the administrative write.
    public static async Task<bool> LockAdministratorAsync(DealershipDbContext database, Guid actorId,
        CancellationToken cancellationToken)
    {
        var actor = await database.Users.FromSqlInterpolated(
            $"SELECT * FROM \"AspNetUsers\" WHERE \"Id\" = {actorId} FOR SHARE")
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        return actor is { IsActive: true, Role: AccountRoles.Administrator };
    }
}

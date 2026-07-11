using Dealership.Application.Accounts;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Identity;

internal enum AdministratorTargetOutcome
{
    Forbidden,
    Replayed,
    NotFound,
    Locked
}

// Administrators are the active Administrator rows locked with the target; null when the caller did not lock them.
internal sealed record AdministratorTargetLock(AdministratorTargetOutcome Outcome, AccountUser? User = null,
    List<AccountUser>? Administrators = null);

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

    // The shared prologue of an administrator change, inside the caller's transaction. The action's identity is the
    // event's, so an applied change is recognised when it is sent again. When the change can remove an Administrator,
    // every active Administrator row is locked in id order first, so two removals can never leave none.
    public static async Task<AdministratorTargetLock> LockTargetAsync(DealershipDbContext database,
        Guid administratorId, Guid targetId, Guid operationId, bool lockAdministrators,
        CancellationToken cancellationToken)
    {
        if (!await LockAdministratorAsync(database, administratorId, cancellationToken))
        {
            return new(AdministratorTargetOutcome.Forbidden);
        }

        if (await database.AccountEvents.AnyAsync(item => item.Id == operationId && item.TargetId == targetId,
                cancellationToken))
        {
            return new(AdministratorTargetOutcome.Replayed);
        }

        List<AccountUser>? administrators = null;
        if (lockAdministrators)
        {
            administrators = await database.Users.FromSqlInterpolated(
                $"SELECT * FROM \"AspNetUsers\" WHERE \"Role\" = {AccountRoles.Administrator} AND \"IsActive\" ORDER BY \"Id\" FOR UPDATE")
                .ToListAsync(cancellationToken);
        }

        var user = administrators?.SingleOrDefault(item => item.Id == targetId) ?? await database.Users
            .FromSqlInterpolated($"SELECT * FROM \"AspNetUsers\" WHERE \"Id\" = {targetId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        return user is null
            ? new(AdministratorTargetOutcome.NotFound)
            : new(AdministratorTargetOutcome.Locked, user, administrators);
    }
}

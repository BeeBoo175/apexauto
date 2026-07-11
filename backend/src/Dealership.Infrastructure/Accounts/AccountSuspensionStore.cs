using System.Data;
using System.Security.Cryptography;
using Dealership.Application.Accounts;
using Dealership.Domain.Accounts;
using Dealership.Infrastructure.Identity;
using Dealership.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Accounts;

public sealed class AccountSuspensionStore(DealershipDbContext database, IPasswordHasher<AccountUser> hasher,
    TimeProvider clock) : IAccountSuspensionStore
{
    public Task<AccountSuspensionStatus> SuspendAsync(Guid administratorId, Guid targetId,
        AccountSuspensionRequest request, CancellationToken cancellationToken) =>
        ChangeAsync(administratorId, targetId, request, suspend: true, cancellationToken);

    public Task<AccountSuspensionStatus> LiftAsync(Guid administratorId, Guid targetId,
        AccountSuspensionRequest request, CancellationToken cancellationToken) =>
        ChangeAsync(administratorId, targetId, request, suspend: false, cancellationToken);

    private async Task<AccountSuspensionStatus> ChangeAsync(Guid administratorId, Guid targetId,
        AccountSuspensionRequest request, bool suspend, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted,
            cancellationToken);
        if (!await AdministratorScope.LockAdministratorAsync(database, administratorId, cancellationToken))
        {
            return AccountSuspensionStatus.Forbidden;
        }

        // The action's identity is the event's, so an applied change is recognised when it is sent again.
        if (await database.AccountEvents.AnyAsync(item => item.Id == request.OperationId && item.TargetId == targetId,
                cancellationToken))
        {
            return AccountSuspensionStatus.Succeeded;
        }

        // Every active Administrator row is locked in a fixed order, so two suspensions can never leave none.
        var administrators = await database.Users.FromSqlInterpolated(
            $"SELECT * FROM \"AspNetUsers\" WHERE \"Role\" = {AccountRoles.Administrator} AND \"IsActive\" ORDER BY \"Id\" FOR UPDATE")
            .ToListAsync(cancellationToken);
        var user = administrators.SingleOrDefault(item => item.Id == targetId) ?? await database.Users
            .FromSqlInterpolated($"SELECT * FROM \"AspNetUsers\" WHERE \"Id\" = {targetId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (user is null)
        {
            return AccountSuspensionStatus.NotFound;
        }

        var allowed = suspend ? user.IsActive : user.SuspendedAt is not null;
        if (!allowed || (suspend && (targetId == administratorId ||
            (user.Role == AccountRoles.Administrator && !administrators.Any(item => item.Id != targetId)))))
        {
            return AccountSuspensionStatus.Conflict;
        }

        var now = Now();
        var changes = new List<AccountChange>
        {
            new("Access", suspend ? "Active" : "Suspended", suspend ? "Suspended" : "Active")
        };
        user.IsActive = !suspend;
        user.SuspendedAt = suspend ? now : null;
        user.SecurityStamp = Guid.NewGuid().ToString();
        if (request.RequirePasswordReset)
        {
            // An unusable replacement: only the recovery flow, once the account is active, sets a password again.
            user.PasswordHash = hasher.HashPassword(user, Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));
            changes.Add(new("Password", "Set", "Reset required"));
        }

        if (request.RequireEmailVerification && user.EmailConfirmed)
        {
            user.EmailConfirmed = false;
            changes.Add(new("Email verification", "Verified", "Required"));
        }

        if (suspend)
        {
            await database.AccountSessions.Where(session => session.UserId == targetId && session.RevokedAt == null)
                .ExecuteUpdateAsync(update => update.SetProperty(session => session.RevokedAt, now), cancellationToken);
            await database.PasswordResetChallenges
                .Where(challenge => challenge.UserId == targetId && challenge.InvalidatedAt == null &&
                    challenge.ConsumedAt == null)
                .ExecuteUpdateAsync(update => update.SetProperty(challenge => challenge.InvalidatedAt, now),
                    cancellationToken);
        }

        database.AccountEvents.Add(new AccountEvent(request.OperationId, targetId, administratorId,
            suspend ? AccountEventKind.Suspended : AccountEventKind.SuspensionLifted, request.Reason, changes, now));
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return AccountSuspensionStatus.Succeeded;
    }

    private DateTimeOffset Now()
    {
        var ticks = clock.GetUtcNow().UtcTicks;
        return new DateTimeOffset(ticks - ticks % 10, TimeSpan.Zero);
    }
}

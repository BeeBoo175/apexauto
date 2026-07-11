using System.Data;
using System.Security.Cryptography;
using Dealership.Application.Accounts;
using Dealership.Domain.Accounts;
using Dealership.Domain.Common;
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
        var locked = await AdministratorScope.LockTargetAsync(database, administratorId, targetId,
            request.OperationId, lockAdministrators: true, cancellationToken);
        switch (locked.Outcome)
        {
            case AdministratorTargetOutcome.Forbidden:
                return AccountSuspensionStatus.Forbidden;
            case AdministratorTargetOutcome.Replayed:
                return AccountSuspensionStatus.Succeeded;
            case AdministratorTargetOutcome.NotFound:
                return AccountSuspensionStatus.NotFound;
        }

        var user = locked.User!;
        var administrators = locked.Administrators!;

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
            await AccountAccessRevocation.RevokeAsync(database, targetId, now, cancellationToken);
        }

        database.AccountEvents.Add(new AccountEvent(request.OperationId, targetId, administratorId,
            suspend ? AccountEventKind.Suspended : AccountEventKind.SuspensionLifted, request.Reason, changes, now));
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return AccountSuspensionStatus.Succeeded;
    }

    private DateTimeOffset Now() => UtcTime.TruncateToMicroseconds(clock.GetUtcNow());
}

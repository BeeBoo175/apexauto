using System.Data;
using System.Security.Cryptography;
using Dealership.Application.Accounts;
using Dealership.Domain.Accounts;
using Dealership.Domain.Common;
using Dealership.Infrastructure.Identity;
using Dealership.Infrastructure.Mail;
using Dealership.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Dealership.Infrastructure.Accounts;

public sealed class AccountEditStore(DealershipDbContext database, IPasswordHasher<AccountUser> hasher,
    TimeProvider clock, MailDeliverySignal mail) : IAccountEditStore
{
    private const string EmailIndex = "EmailIndex";

    public async Task<AccountEditStatus> EditAsync(Guid administratorId, Guid targetId, ValidatedAccountEdit edit,
        CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted,
            cancellationToken);
        var locked = await AdministratorScope.LockTargetAsync(database, administratorId, targetId,
            edit.OperationId, lockAdministrators: false, cancellationToken);
        switch (locked.Outcome)
        {
            case AdministratorTargetOutcome.Forbidden:
                return AccountEditStatus.Forbidden;
            case AdministratorTargetOutcome.Replayed:
                return AccountEditStatus.Succeeded;
            case AdministratorTargetOutcome.NotFound:
                return AccountEditStatus.NotFound;
        }

        var user = locked.User!;

        if (targetId == administratorId)
        {
            return AccountEditStatus.Conflict;
        }

        var customer = user.Role == AccountRoles.Customer;
        if (customer != (edit.Address is not null))
        {
            return AccountEditStatus.Invalid;
        }

        var changes = customer ? await EditCustomerAsync(user, edit, cancellationToken)
            : await EditStaffAsync(user, edit, cancellationToken);
        if (changes is null)
        {
            return AccountEditStatus.NotFound;
        }

        var now = Now();
        var emailChanged = !string.Equals(user.Email, edit.Email, StringComparison.Ordinal);
        if (emailChanged)
        {
            if (await EmailBelongsToAnotherAsync(user.Id, edit.Email, cancellationToken))
            {
                return AccountEditStatus.Conflict;
            }

            changes.Add(new("Email", user.Email, edit.Email));
            await ApplyEmailChangeAsync(user, edit, changes, now, cancellationToken);
        }

        if (changes.Count == 0)
        {
            return AccountEditStatus.Conflict;
        }

        database.AccountEvents.Add(new AccountEvent(edit.OperationId, targetId, administratorId,
            emailChanged ? AccountEventKind.EmailChanged : AccountEventKind.ProfileEdited, edit.Reason, changes, now));
        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: EmailIndex })
        {
            database.ChangeTracker.Clear();
            return AccountEditStatus.Conflict;
        }

        await transaction.CommitAsync(cancellationToken);
        if (emailChanged && edit.RequireEmailVerification && user.IsActive)
        {
            mail.Request();
        }

        return AccountEditStatus.Succeeded;
    }

    private async Task<List<AccountChange>?> EditCustomerAsync(AccountUser user, ValidatedAccountEdit edit,
        CancellationToken cancellationToken)
    {
        var profile = await database.CustomerProfiles.SingleOrDefaultAsync(item => item.UserId == user.Id,
            cancellationToken);
        if (profile is null)
        {
            return null;
        }

        var changes = new List<AccountChange>();
        Track(changes, "Name", profile.LegalName, edit.Name);
        Track(changes, "Mobile", profile.Mobile, edit.Mobile);
        Track(changes, "Address", profile.ResidentialAddress, edit.Address);
        profile.Update(edit.Name, edit.Mobile, edit.Address!);
        return changes;
    }

    private async Task<List<AccountChange>?> EditStaffAsync(AccountUser user, ValidatedAccountEdit edit,
        CancellationToken cancellationToken)
    {
        var profile = await database.StaffProfiles.SingleOrDefaultAsync(item => item.UserId == user.Id,
            cancellationToken);
        if (profile is null)
        {
            return null;
        }

        var changes = new List<AccountChange>();
        Track(changes, "Name", profile.Name, edit.Name);
        Track(changes, "Mobile", profile.Mobile, edit.Mobile);
        profile.Update(edit.Name, edit.Mobile);
        return changes;
    }

    // The new address applies at once. Sessions and unused links tied to the old address end with it.
    private async Task ApplyEmailChangeAsync(AccountUser user, ValidatedAccountEdit edit, List<AccountChange> changes,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        user.Email = edit.Email;
        user.UserName = edit.Email;
        user.NormalizedEmail = edit.Email.ToUpperInvariant();
        user.NormalizedUserName = user.NormalizedEmail;
        user.SecurityStamp = Guid.NewGuid().ToString();
        await AccountAccessRevocation.RevokeAsync(database, user.Id, now, cancellationToken);
        await database.EmailVerificationChallenges
            .Where(challenge => challenge.UserId == user.Id && challenge.InvalidatedAt == null &&
                challenge.ConsumedAt == null)
            .ExecuteUpdateAsync(update => update.SetProperty(challenge => challenge.InvalidatedAt, now),
                cancellationToken);

        if (edit.RequirePasswordReset)
        {
            // An unusable replacement: only the recovery flow sets a password again.
            user.PasswordHash = hasher.HashPassword(user, Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));
            changes.Add(new("Password", "Set", "Reset required"));
        }

        if (edit.RequireEmailVerification)
        {
            changes.Add(new("Email verification", user.EmailConfirmed ? "Verified" : "Unverified", "Required"));
            user.EmailConfirmed = false;

            // An inactive account has no link to confirm; verification is asked for again once it is active.
            if (user.IsActive)
            {
                database.EmailVerificationChallenges.Add(EmailVerificationChallenge.Create(user, null, now));
            }
        }
    }

    private async Task<bool> EmailBelongsToAnotherAsync(Guid userId, string email, CancellationToken cancellationToken)
    {
        var normalized = email.ToUpperInvariant();
        return await database.Users.AsNoTracking().AnyAsync(account => account.Id != userId &&
                (account.NormalizedEmail == normalized || account.NormalizedUserName == normalized), cancellationToken) ||
            await database.StaffInvitations.AsNoTracking().AnyAsync(invitation => invitation.ActivatedAt == null &&
                invitation.RevokedAt == null && EF.Property<string>(invitation, "NormalizedEmail") == normalized, cancellationToken);
    }

    private static void Track(List<AccountChange> changes, string field, string before, string? after)
    {
        if (!string.Equals(before, after, StringComparison.Ordinal))
        {
            changes.Add(new(field, before, after));
        }
    }

    private DateTimeOffset Now() => UtcTime.TruncateToMicroseconds(clock.GetUtcNow());
}

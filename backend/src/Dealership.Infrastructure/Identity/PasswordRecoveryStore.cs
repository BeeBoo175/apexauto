using Dealership.Application.Accounts;
using Dealership.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Dealership.Infrastructure.Mail;

namespace Dealership.Infrastructure.Identity;

public sealed class PasswordRecoveryStore(DealershipDbContext database, UserManager<AccountUser> users,
    PasswordResetToken tokens, TimeProvider clock, MailDeliverySignal mail) : IPasswordRecoveryStore
{
    public async Task<IPasswordRecoveryRequestTransaction> LockRequestAsync(string email, CancellationToken cancellationToken)
    {
        var normalized = users.NormalizeEmail(email);
        var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var user = await database.Users.FromSqlInterpolated(
                $"SELECT * FROM \"AspNetUsers\" WHERE \"NormalizedEmail\" = {normalized} FOR UPDATE")
                .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
            var context = new PasswordRecoveryRequestContext(user is not null);
            return new PasswordRecoveryRequestTransaction(database, transaction, context, user, mail);
        }
        catch
        {
            try
            {
                await transaction.DisposeAsync();
            }
            finally
            {
                database.ChangeTracker.Clear();
            }

            throw;
        }
    }

    public async Task<PasswordResetResult> ResetAsync(string token, string password, CancellationToken cancellationToken)
    {
        if (!PasswordResetToken.TryReadId(token, out var id))
        {
            return new PasswordResetResult(PasswordResetStatus.Invalid);
        }

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var userId = await database.PasswordResetChallenges.Where(challenge => challenge.Id == id)
            .Select(challenge => (Guid?)challenge.UserId).SingleOrDefaultAsync(cancellationToken);

        if (userId is null)
        {
            return new PasswordResetResult(PasswordResetStatus.Invalid);
        }

        // This is the login lock too: no old-credential session can commit after the reset.
        var user = await database.Users.FromSqlInterpolated(
            $"SELECT * FROM \"AspNetUsers\" WHERE \"Id\" = {userId.Value} FOR UPDATE")
            .SingleAsync(cancellationToken);
        var challenge = await database.PasswordResetChallenges.FromSqlInterpolated(
            $"SELECT * FROM \"PasswordResetChallenges\" WHERE \"Id\" = {id} FOR UPDATE")
            .SingleAsync(cancellationToken);
        var now = clock.GetUtcNow();

        if (!tokens.Verify(token, challenge) || challenge.InvalidatedAt is not null || challenge.ExpiresAt <= now ||
            !string.Equals(user.Email, challenge.RecipientEmail, StringComparison.Ordinal))
        {
            return new PasswordResetResult(PasswordResetStatus.Invalid);
        }

        if (challenge.ConsumedAt is not null)
        {
            return Replay(user, challenge, password);
        }

        var errors = await ValidatePasswordAsync(user, password);

        if (errors.Length > 0)
        {
            return new PasswordResetResult(PasswordResetStatus.PasswordRejected, errors);
        }

        user.PasswordHash = users.PasswordHasher.HashPassword(user, password);
        user.SecurityStamp = Guid.NewGuid().ToString();
        var updated = await users.UpdateAsync(user);

        if (!updated.Succeeded)
        {
            throw new InvalidOperationException("Identity could not update the recovered credentials.");
        }

        await database.AccountSessions.Where(session => session.UserId == user.Id && session.RevokedAt == null)
            .ExecuteUpdateAsync(update => update.SetProperty(session => session.RevokedAt, now), cancellationToken);
        challenge.ConsumedAt = now;
        challenge.CompletedSecurityStamp = user.SecurityStamp;
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new PasswordResetResult(PasswordResetStatus.Succeeded);
    }

    private PasswordResetResult Replay(AccountUser user, PasswordResetChallenge challenge, string password)
    {
        // Verify the current slow Identity hash without CheckPasswordAsync's possible rehash/write.
        var matches = challenge.CompletedSecurityStamp == user.SecurityStamp && user.PasswordHash is not null &&
            users.PasswordHasher.VerifyHashedPassword(user, user.PasswordHash, password) != PasswordVerificationResult.Failed;
        return new PasswordResetResult(matches ? PasswordResetStatus.Succeeded : PasswordResetStatus.Invalid);
    }

    private async Task<string[]> ValidatePasswordAsync(AccountUser user, string password)
    {
        var errors = new List<string>();

        foreach (var validator in users.PasswordValidators)
        {
            var result = await validator.ValidateAsync(users, user, password);

            if (!result.Succeeded)
            {
                var descriptions = result.Errors.Select(error => error.Description).ToArray();
                errors.AddRange(descriptions.Length > 0 ? descriptions : ["The password does not meet account policy."]);
            }
        }

        return [.. errors.Distinct(StringComparer.Ordinal)];
    }
}

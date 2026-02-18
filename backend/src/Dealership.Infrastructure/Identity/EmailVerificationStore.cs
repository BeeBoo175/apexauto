using Dealership.Application.Accounts;
using Dealership.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Dealership.Infrastructure.Mail;

namespace Dealership.Infrastructure.Identity;

public sealed class EmailVerificationStore(DealershipDbContext database, UserManager<AccountUser> users,
    EmailVerificationToken tokens, TimeProvider clock, MailDeliverySignal mail) : IEmailVerificationStore
{
    public async Task QueueInitialAsync(Guid userId, CancellationToken cancellationToken)
    {
        // Registration owns the surrounding transaction; the mail intent commits with the account.
        var user = await database.Users.SingleAsync(account => account.Id == userId, cancellationToken);
        AddChallenge(user, null);
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<VerificationResult> ResendAsync(Guid userId, Guid resendKey, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var user = await LockedCustomerAsync(userId, cancellationToken);

        if (user is null)
        {
            return new VerificationResult(VerificationStatus.Forbidden);
        }

        var replayOwner = await database.EmailVerificationChallenges
            .Where(challenge => challenge.ResendKey == resendKey)
            .Select(challenge => (Guid?)challenge.UserId).SingleOrDefaultAsync(cancellationToken);

        if (replayOwner is not null)
        {
            return new VerificationResult(replayOwner == userId ? VerificationStatus.Accepted : VerificationStatus.Forbidden);
        }

        if (user.EmailConfirmed)
        {
            return new VerificationResult(VerificationStatus.Accepted);
        }

        var now = clock.GetUtcNow();
        var recent = await database.EmailVerificationChallenges
            .Where(challenge => challenge.UserId == userId && challenge.ResendKey != null &&
                challenge.CreatedAt > now - EmailVerificationPolicy.ResendWindow)
            .OrderByDescending(challenge => challenge.CreatedAt)
            .Select(challenge => challenge.CreatedAt)
            .ToListAsync(cancellationToken);

        var delay = EmailVerificationPolicy.ResendDelay(now, recent);

        if (delay is not null)
        {
            return new VerificationResult(VerificationStatus.RateLimited, delay);
        }

        await InvalidateOutstandingAsync(userId, now, cancellationToken);
        AddChallenge(user, resendKey);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        mail.Request();
        return new VerificationResult(VerificationStatus.Accepted);
    }

    public async Task<VerificationResult> ConfirmAsync(string token, CancellationToken cancellationToken)
    {
        if (!EmailVerificationToken.TryReadId(token, out var challengeId))
        {
            return new VerificationResult(VerificationStatus.Invalid);
        }

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var userId = await database.EmailVerificationChallenges.Where(challenge => challenge.Id == challengeId)
            .Select(challenge => (Guid?)challenge.UserId).SingleOrDefaultAsync(cancellationToken);

        if (userId is null)
        {
            return new VerificationResult(VerificationStatus.Invalid);
        }

        var user = await LockedCustomerAsync(userId.Value, cancellationToken);

        if (user is null)
        {
            return new VerificationResult(VerificationStatus.Invalid);
        }

        var challenge = await database.EmailVerificationChallenges.FromSqlInterpolated(
            $"SELECT * FROM \"EmailVerificationChallenges\" WHERE \"Id\" = {challengeId} FOR UPDATE")
            .SingleAsync(cancellationToken);

        if (!tokens.Verify(token, challenge) || challenge.InvalidatedAt is not null ||
            !string.Equals(user.Email, challenge.RecipientEmail, StringComparison.Ordinal))
        {
            return new VerificationResult(VerificationStatus.Invalid);
        }

        var now = clock.GetUtcNow();

        if (challenge.ExpiresAt <= now)
        {
            return new VerificationResult(VerificationStatus.Invalid);
        }

        if (challenge.ConsumedAt is not null)
        {
            return new VerificationResult(user.EmailConfirmed ? VerificationStatus.Accepted : VerificationStatus.Invalid);
        }

        if (user.EmailConfirmed)
        {
            return new VerificationResult(VerificationStatus.Invalid);
        }

        user.EmailConfirmed = true;
        var confirmed = await users.UpdateAsync(user);

        if (!confirmed.Succeeded)
        {
            throw new InvalidOperationException("Identity could not confirm the account email.");
        }

        challenge.ConsumedAt = now;
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new VerificationResult(VerificationStatus.Accepted);
    }

    private async Task<AccountUser?> LockedCustomerAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await database.Users.FromSqlInterpolated(
            $"SELECT * FROM \"AspNetUsers\" WHERE \"Id\" = {userId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

        if (user is null || !user.IsActive || user.Role != AccountRoles.Customer ||
            !await database.CustomerProfiles.AnyAsync(profile => profile.UserId == userId, cancellationToken))
        {
            return null;
        }

        return user;
    }

    private async Task InvalidateOutstandingAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await database.EmailVerificationChallenges
            .Where(challenge => challenge.UserId == userId && challenge.InvalidatedAt == null && challenge.ConsumedAt == null)
            .ExecuteUpdateAsync(update => update.SetProperty(challenge => challenge.InvalidatedAt, now), cancellationToken);
    }

    private void AddChallenge(AccountUser user, Guid? resendKey)
    {
        var now = clock.GetUtcNow();
        database.EmailVerificationChallenges.Add(new EmailVerificationChallenge
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            RecipientEmail = user.Email!,
            CreatedAt = now,
            ExpiresAt = now + EmailVerificationPolicy.Lifetime,
            NextDeliveryAt = now,
            ResendKey = resendKey
        });
    }
}

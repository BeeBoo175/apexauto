using Dealership.Infrastructure.Mail;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Identity;

public sealed class PasswordResetMailSource(DealershipDbContext database, PasswordResetMailComposer composer,
    TimeProvider clock) : IMailDeliverySource
{
    public async Task<ClaimedMail?> ClaimNextAsync(CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var now = clock.GetUtcNow();
        var challenge = await database.PasswordResetChallenges.FromSqlInterpolated($"""
            SELECT * FROM "PasswordResetChallenges"
            WHERE "DeliveredAt" IS NULL AND "InvalidatedAt" IS NULL AND "ConsumedAt" IS NULL
              AND "ExpiresAt" > {now} AND "NextDeliveryAt" <= {now}
              AND ("DeliveryClaimExpiresAt" IS NULL OR "DeliveryClaimExpiresAt" <= {now})
            ORDER BY "NextDeliveryAt", "CreatedAt", "Id"
            LIMIT 1 FOR UPDATE SKIP LOCKED
            """).SingleOrDefaultAsync(cancellationToken);

        if (challenge is null)
        {
            return null;
        }

        var claim = MailDeliveryClaim.Create(now, challenge.DeliveryAttempts);
        challenge.DeliveryClaimId = claim.Id;
        challenge.DeliveryClaimExpiresAt = claim.ExpiresAt;
        challenge.DeliveryAttempts = claim.Attempts;
        challenge.NextDeliveryAt = claim.RetryAt;
        var message = composer.Compose(challenge);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        database.Entry(challenge).State = EntityState.Detached;
        return new ClaimedMail(challenge.Id, claim.Id, message);
    }

    public async Task MarkDeliveredAsync(Guid messageId, Guid claimId, CancellationToken cancellationToken)
    {
        await database.PasswordResetChallenges
            .Where(challenge => challenge.Id == messageId && challenge.DeliveryClaimId == claimId &&
                challenge.DeliveredAt == null)
            .ExecuteUpdateAsync(update => update.SetProperty(challenge => challenge.DeliveredAt, clock.GetUtcNow()),
                cancellationToken);
    }
}

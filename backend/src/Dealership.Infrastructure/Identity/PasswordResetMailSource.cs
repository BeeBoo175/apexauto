using Dealership.Infrastructure.Mail;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Identity;

public sealed class PasswordResetMailSource(DealershipDbContext database, PasswordResetMailComposer composer,
    TimeProvider clock) : IMailDeliverySource
{
    public Task<ClaimedMail?> ClaimNextAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var candidates = database.PasswordResetChallenges.FromSqlInterpolated($"""
            SELECT * FROM "PasswordResetChallenges"
            WHERE "DeliveredAt" IS NULL AND "RecipientEmail" IS NOT NULL AND "InvalidatedAt" IS NULL AND "ConsumedAt" IS NULL
              AND "ExpiresAt" > {now} AND "NextDeliveryAt" <= {now}
              AND ("DeliveryClaimExpiresAt" IS NULL OR "DeliveryClaimExpiresAt" <= {now})
            ORDER BY "NextDeliveryAt", "CreatedAt", "Id"
            LIMIT 1 FOR UPDATE SKIP LOCKED
            """);

        return MailDeliveryBookkeeping.ClaimNextAsync(database, candidates, now,
            (challenge, _) => Task.FromResult(composer.Compose(challenge)), cancellationToken);
    }

    public Task MarkDeliveredAsync(Guid messageId, Guid claimId, CancellationToken cancellationToken)
    {
        return MailDeliveryBookkeeping.MarkDeliveredAsync(database.PasswordResetChallenges, messageId, claimId,
            clock.GetUtcNow(), cancellationToken);
    }
}

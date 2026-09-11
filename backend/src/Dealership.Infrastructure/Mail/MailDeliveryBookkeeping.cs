using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Mail;

public static class MailDeliveryBookkeeping
{
    public static async Task<ClaimedMail?> ClaimNextAsync<T>(DealershipDbContext database,
        IQueryable<T> candidates, DateTimeOffset now, Func<T, CancellationToken, Task<OutboundMail>> compose,
        CancellationToken cancellationToken) where T : class, IMailDeliveryState
    {
        // The source supplies its eligibility query, including row locking and purpose-specific suppression.
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var intent = await candidates.SingleOrDefaultAsync(cancellationToken);

        if (intent is null)
        {
            return null;
        }

        var claim = MailDeliveryClaim.Create(now, intent.DeliveryAttempts);
        intent.DeliveryClaimId = claim.Id;
        intent.DeliveryClaimExpiresAt = claim.ExpiresAt;
        intent.DeliveryAttempts = claim.Attempts;
        intent.NextDeliveryAt = claim.RetryAt;
        OutboundMail message;

        try
        {
            message = await compose(intent, cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // Keep the claim and its backoff so a message that cannot be composed stops heading its queue.
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            database.Entry(intent).State = EntityState.Detached;
            throw;
        }

        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        database.Entry(intent).State = EntityState.Detached;
        return new ClaimedMail(intent.Id, claim.Id, message);
    }

    public static async Task MarkDeliveredAsync<T>(IQueryable<T> intents, Guid messageId, Guid claimId,
        DateTimeOffset now, CancellationToken cancellationToken) where T : class, IMailDeliveryState
    {
        await intents.Where(intent => intent.Id == messageId && intent.DeliveryClaimId == claimId &&
                intent.DeliveredAt == null)
            .ExecuteUpdateAsync(update => update.SetProperty(intent => intent.DeliveredAt, now), cancellationToken);
    }
}

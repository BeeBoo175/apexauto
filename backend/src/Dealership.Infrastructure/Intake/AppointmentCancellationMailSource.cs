using Dealership.Infrastructure.Mail;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Intake;

public sealed class AppointmentCancellationMailSource(DealershipDbContext database, TimeProvider clock) : IMailDeliverySource
{
    public async Task<ClaimedMail?> ClaimNextAsync(CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var now = clock.GetUtcNow();
        var message = await database.AppointmentCancellationMails.FromSqlInterpolated($"""
            SELECT * FROM "AppointmentCancellationMails"
            WHERE "DeliveredAt" IS NULL AND "NextDeliveryAt" <= {now}
              AND ("DeliveryClaimExpiresAt" IS NULL OR "DeliveryClaimExpiresAt" <= {now})
            ORDER BY "NextDeliveryAt", "Id" LIMIT 1 FOR UPDATE SKIP LOCKED
            """).SingleOrDefaultAsync(cancellationToken);

        if (message is null)
        {
            return null;
        }

        var claim = MailDeliveryClaim.Create(now, message.DeliveryAttempts);
        message.DeliveryClaimId = claim.Id;
        message.DeliveryClaimExpiresAt = claim.ExpiresAt;
        message.DeliveryAttempts = claim.Attempts;
        message.NextDeliveryAt = claim.RetryAt;
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        database.Entry(message).State = EntityState.Detached;
        var body = $"Your ApexAuto inspection booking is cancelled.\nReference: {message.Id:D}\nTo book again, repeat the vehicle intake process.";
        return new ClaimedMail(message.Id, claim.Id,
            new OutboundMail(message.RecipientEmail, "ApexAuto inspection booking cancelled", body));
    }

    public async Task MarkDeliveredAsync(Guid messageId, Guid claimId, CancellationToken cancellationToken)
    {
        await database.AppointmentCancellationMails.Where(message => message.Id == messageId &&
            message.DeliveryClaimId == claimId && message.DeliveredAt == null)
            .ExecuteUpdateAsync(update => update.SetProperty(message => message.DeliveredAt, clock.GetUtcNow()), cancellationToken);
    }
}

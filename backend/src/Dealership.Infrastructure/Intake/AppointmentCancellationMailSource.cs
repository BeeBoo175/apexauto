using Dealership.Infrastructure.Mail;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Intake;

public sealed class AppointmentCancellationMailSource(DealershipDbContext database, TimeProvider clock) : IMailDeliverySource
{
    public Task<ClaimedMail?> ClaimNextAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var candidates = database.AppointmentCancellationMails.FromSqlInterpolated($"""
            SELECT * FROM "AppointmentCancellationMails"
            WHERE "DeliveredAt" IS NULL AND "NextDeliveryAt" <= {now}
              AND ("DeliveryClaimExpiresAt" IS NULL OR "DeliveryClaimExpiresAt" <= {now})
            ORDER BY "NextDeliveryAt", "Id" LIMIT 1 FOR UPDATE SKIP LOCKED
            """);

        return MailDeliveryBookkeeping.ClaimNextAsync(database, candidates, now,
            (message, _) => Task.FromResult(AppointmentMailComposer.Cancellation(message)), cancellationToken);
    }

    public Task MarkDeliveredAsync(Guid messageId, Guid claimId, CancellationToken cancellationToken)
    {
        return MailDeliveryBookkeeping.MarkDeliveredAsync(database.AppointmentCancellationMails, messageId, claimId,
            clock.GetUtcNow(), cancellationToken);
    }
}

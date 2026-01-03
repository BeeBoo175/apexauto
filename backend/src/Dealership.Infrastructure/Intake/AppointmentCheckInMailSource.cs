using Dealership.Infrastructure.Mail;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Intake;

public sealed class AppointmentCheckInMailSource(DealershipDbContext database, TimeProvider clock) : IMailDeliverySource
{
    public Task<ClaimedMail?> ClaimNextAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var candidates = database.AppointmentCheckInMails.FromSqlInterpolated($"""
            SELECT * FROM "AppointmentCheckInMails"
            WHERE "DeliveredAt" IS NULL AND "NextDeliveryAt" <= {now}
              AND ("DeliveryClaimExpiresAt" IS NULL OR "DeliveryClaimExpiresAt" <= {now})
            ORDER BY "NextDeliveryAt", "Id" LIMIT 1 FOR UPDATE SKIP LOCKED
            """);

        return MailDeliveryBookkeeping.ClaimNextAsync(database, candidates, now, ComposeAsync, cancellationToken);
    }

    private async Task<OutboundMail> ComposeAsync(AppointmentCheckInMail message, CancellationToken cancellationToken)
    {
        var appointment = await database.IntakeAppointments.AsNoTracking()
            .SingleAsync(item => item.Id == message.Id, cancellationToken);
        return AppointmentMailComposer.CheckIn(message, appointment);
    }

    public Task MarkDeliveredAsync(Guid messageId, Guid claimId, CancellationToken cancellationToken)
    {
        return MailDeliveryBookkeeping.MarkDeliveredAsync(database.AppointmentCheckInMails, messageId, claimId,
            clock.GetUtcNow(), cancellationToken);
    }
}

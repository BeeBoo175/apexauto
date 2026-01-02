using Dealership.Infrastructure.Mail;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Intake;

public sealed class AppointmentConfirmationSource(DealershipDbContext database, TimeProvider clock) : IMailDeliverySource
{
    public Task<ClaimedMail?> ClaimNextAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var candidates = database.AppointmentConfirmations.FromSqlInterpolated($"""
            SELECT * FROM "AppointmentConfirmations"
            WHERE "DeliveredAt" IS NULL AND "NextDeliveryAt" <= {now}
              AND ("DeliveryClaimExpiresAt" IS NULL OR "DeliveryClaimExpiresAt" <= {now})
              AND NOT EXISTS (SELECT 1 FROM "AppointmentCancellations" c WHERE c."AppointmentId" = "AppointmentConfirmations"."Id")
            ORDER BY "NextDeliveryAt", "Id" LIMIT 1 FOR UPDATE SKIP LOCKED
            """);

        return MailDeliveryBookkeeping.ClaimNextAsync(database, candidates, now, ComposeAsync, cancellationToken);
    }

    private async Task<OutboundMail> ComposeAsync(AppointmentConfirmation message, CancellationToken cancellationToken)
    {
        var appointment = await database.IntakeAppointments.AsNoTracking().SingleAsync(item => item.Id == message.Id, cancellationToken);
        var branch = await database.Branches.AsNoTracking().SingleAsync(item => item.Id == appointment.BranchId, cancellationToken);
        return AppointmentMailComposer.Confirmation(message, appointment, branch.Name);
    }

    public Task MarkDeliveredAsync(Guid messageId, Guid claimId, CancellationToken cancellationToken)
    {
        return MailDeliveryBookkeeping.MarkDeliveredAsync(database.AppointmentConfirmations, messageId, claimId,
            clock.GetUtcNow(), cancellationToken);
    }
}

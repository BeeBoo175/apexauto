using Dealership.Infrastructure.Mail;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Intake;

public sealed class AppointmentMailSource(DealershipDbContext database, TimeProvider clock) : IMailDeliverySource
{
    public Task<ClaimedMail?> ClaimNextAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var candidates = database.AppointmentMailOutbox.FromSqlInterpolated($"""
            SELECT m.* FROM "AppointmentMailOutbox" m
            WHERE m."DeliveredAt" IS NULL AND m."NextDeliveryAt" <= {now}
              AND (m."DeliveryClaimExpiresAt" IS NULL OR m."DeliveryClaimExpiresAt" <= {now})
              AND (m."Kind" <> 'Confirmation' OR NOT EXISTS (
                  SELECT 1 FROM "AppointmentCancellations" c WHERE c."AppointmentId" = m."AppointmentId"))
            ORDER BY m."NextDeliveryAt", m."Id" LIMIT 1 FOR UPDATE OF m SKIP LOCKED
            """);

        return MailDeliveryBookkeeping.ClaimNextAsync(database, candidates, now, ComposeAsync, cancellationToken);
    }

    public Task MarkDeliveredAsync(Guid messageId, Guid claimId, CancellationToken cancellationToken)
    {
        return MailDeliveryBookkeeping.MarkDeliveredAsync(database.AppointmentMailOutbox, messageId, claimId,
            clock.GetUtcNow(), cancellationToken);
    }

    private async Task<OutboundMail> ComposeAsync(AppointmentMail message, CancellationToken cancellationToken)
    {
        if (message.Kind == AppointmentMailKind.Cancellation)
        {
            return AppointmentMailComposer.Cancellation(message);
        }

        var appointment = await database.IntakeAppointments.AsNoTracking()
            .SingleAsync(item => item.Id == message.AppointmentId, cancellationToken);

        if (message.Kind == AppointmentMailKind.CheckIn)
        {
            return AppointmentMailComposer.CheckIn(message, appointment);
        }

        var branch = await database.Branches.AsNoTracking()
            .SingleAsync(item => item.Id == appointment.BranchId, cancellationToken);
        return AppointmentMailComposer.Confirmation(message, appointment, branch.Name);
    }
}

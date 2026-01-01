using Dealership.Domain.Common;
using Dealership.Infrastructure.Mail;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Intake;

public sealed class AppointmentConfirmationSource(DealershipDbContext database, TimeProvider clock) : IMailDeliverySource
{
    private static readonly TimeZoneInfo Malaysia = TimeZoneInfo.FindSystemTimeZoneById(DealershipConstants.MalaysiaTimeZoneId);

    public async Task<ClaimedMail?> ClaimNextAsync(CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var now = clock.GetUtcNow();
        var message = await database.AppointmentConfirmations.FromSqlInterpolated($"""
            SELECT * FROM "AppointmentConfirmations"
            WHERE "DeliveredAt" IS NULL AND "NextDeliveryAt" <= {now}
              AND ("DeliveryClaimExpiresAt" IS NULL OR "DeliveryClaimExpiresAt" <= {now})
            ORDER BY "NextDeliveryAt", "Id" LIMIT 1 FOR UPDATE SKIP LOCKED
            """).SingleOrDefaultAsync(cancellationToken);

        if (message is null)
        {
            return null;
        }

        var appointment = await database.IntakeAppointments.AsNoTracking().SingleAsync(item => item.Id == message.Id, cancellationToken);
        var branch = await database.Branches.AsNoTracking().SingleAsync(item => item.Id == appointment.BranchId, cancellationToken);
        var claim = MailDeliveryClaim.Create(now, message.DeliveryAttempts);
        message.DeliveryClaimId = claim.Id;
        message.DeliveryClaimExpiresAt = claim.ExpiresAt;
        message.DeliveryAttempts = claim.Attempts;
        message.NextDeliveryAt = claim.RetryAt;
        var startsAt = TimeZoneInfo.ConvertTime(appointment.StartsAt, Malaysia);
        var body = FormattableString.Invariant($"Your ApexAuto inspection booking is confirmed.\nReference: {appointment.Id:D}\nBranch: {branch.Name}\nStarts: {startsAt:yyyy-MM-dd HH:mm} (Asia/Kuala_Lumpur)\nRegistration: {appointment.RegistrationPlate}");
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        database.Entry(message).State = EntityState.Detached;
        return new ClaimedMail(message.Id, claim.Id, new OutboundMail(message.RecipientEmail, "ApexAuto inspection booking confirmed", body));
    }

    public async Task MarkDeliveredAsync(Guid messageId, Guid claimId, CancellationToken cancellationToken)
    {
        await database.AppointmentConfirmations.Where(message => message.Id == messageId &&
            message.DeliveryClaimId == claimId && message.DeliveredAt == null)
            .ExecuteUpdateAsync(update => update.SetProperty(message => message.DeliveredAt, clock.GetUtcNow()), cancellationToken);
    }
}

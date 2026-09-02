using Dealership.Infrastructure.Mail;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Vehicles;

public sealed class ReservationMailSource(DealershipDbContext database, TimeProvider clock) : IMailDeliverySource
{
    public Task<ClaimedMail?> ClaimNextAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var candidates = database.ReservationMailOutbox.FromSqlInterpolated($"""
            SELECT * FROM "ReservationMailOutbox"
            WHERE "DeliveredAt" IS NULL AND "NextDeliveryAt" <= {now}
              AND ("DeliveryClaimExpiresAt" IS NULL OR "DeliveryClaimExpiresAt" <= {now})
            ORDER BY "NextDeliveryAt", "Id" LIMIT 1 FOR UPDATE SKIP LOCKED
            """);

        return MailDeliveryBookkeeping.ClaimNextAsync(database, candidates, now, ComposeAsync, cancellationToken);
    }

    public Task MarkDeliveredAsync(Guid messageId, Guid claimId, CancellationToken cancellationToken)
    {
        return MailDeliveryBookkeeping.MarkDeliveredAsync(database.ReservationMailOutbox, messageId, claimId,
            clock.GetUtcNow(), cancellationToken);
    }

    private async Task<OutboundMail> ComposeAsync(ReservationMail message, CancellationToken cancellationToken)
    {
        var reservation = await database.Reservations.AsNoTracking()
            .SingleAsync(item => item.Id == message.ReservationId, cancellationToken);
        // The reservation id is its deposit request's id, which keys the hosted payment when there was one.
        var payment = await database.ReservationDepositPayments.AsNoTracking()
            .SingleOrDefaultAsync(item => item.RequestId == reservation.Id, cancellationToken);
        return ReservationMailComposer.Confirmation(reservation, payment);
    }
}

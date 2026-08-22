using Dealership.Infrastructure.Mail;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Vehicles;

public sealed class ReservationRefundMailSource(DealershipDbContext database, TimeProvider clock) : IMailDeliverySource
{
    public Task<ClaimedMail?> ClaimNextAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var candidates = database.ReservationRefundMailOutbox.FromSqlInterpolated($"""
            SELECT * FROM "ReservationRefundMailOutbox"
            WHERE "DeliveredAt" IS NULL AND "NextDeliveryAt" <= {now}
              AND ("DeliveryClaimExpiresAt" IS NULL OR "DeliveryClaimExpiresAt" <= {now})
            ORDER BY "NextDeliveryAt", "Id" LIMIT 1 FOR UPDATE SKIP LOCKED
            """);

        return MailDeliveryBookkeeping.ClaimNextAsync(database, candidates, now, ComposeAsync, cancellationToken);
    }

    public Task MarkDeliveredAsync(Guid messageId, Guid claimId, CancellationToken cancellationToken)
    {
        return MailDeliveryBookkeeping.MarkDeliveredAsync(database.ReservationRefundMailOutbox, messageId, claimId,
            clock.GetUtcNow(), cancellationToken);
    }

    private async Task<OutboundMail> ComposeAsync(ReservationRefundMail message, CancellationToken cancellationToken)
    {
        var refund = await database.ReservationRefunds.AsNoTracking()
            .SingleAsync(item => item.PaymentRequestId == message.RefundId, cancellationToken);
        var request = await database.ReservationDepositRequests.AsNoTracking()
            .SingleAsync(item => item.Id == refund.PaymentRequestId, cancellationToken);
        var vehicle = await database.Vehicles.AsNoTracking()
            .SingleAsync(item => item.Id == request.VehicleId, cancellationToken);
        var email = await database.Users.AsNoTracking().Where(user => user.Id == refund.CustomerId)
            .Select(user => user.Email).SingleAsync(cancellationToken);
        return ReservationRefundMailComposer.Completed(refund, vehicle, email!);
    }
}

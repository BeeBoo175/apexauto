using Dealership.Infrastructure.Identity;
using Dealership.Infrastructure.Mail;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Vehicles;

public sealed class ReservationUnmatchedPaymentMailSource(DealershipDbContext database, TimeProvider clock,
    AuthMailLinks links) : IMailDeliverySource
{
    public Task<ClaimedMail?> ClaimNextAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var candidates = database.ReservationUnmatchedPaymentMailOutbox.FromSqlInterpolated($"""
            SELECT * FROM "ReservationUnmatchedPaymentMailOutbox"
            WHERE "DeliveredAt" IS NULL AND "NextDeliveryAt" <= {now}
              AND ("DeliveryClaimExpiresAt" IS NULL OR "DeliveryClaimExpiresAt" <= {now})
              AND NOT EXISTS (
                SELECT 1 FROM "ReservationDepositRequests" AS request
                JOIN "AspNetUsers" AS owner ON owner."Id" = request."CustomerId"
                WHERE request."Id" = "ReservationUnmatchedPaymentMailOutbox"."RequestId"
                  AND owner."ClosedAt" IS NOT NULL)
            ORDER BY "NextDeliveryAt", "Id" LIMIT 1 FOR UPDATE SKIP LOCKED
            """);

        return MailDeliveryBookkeeping.ClaimNextAsync(database, candidates, now, ComposeAsync, cancellationToken);
    }

    public Task MarkDeliveredAsync(Guid messageId, Guid claimId, CancellationToken cancellationToken)
    {
        return MailDeliveryBookkeeping.MarkDeliveredAsync(database.ReservationUnmatchedPaymentMailOutbox, messageId,
            claimId, clock.GetUtcNow(), cancellationToken);
    }

    private async Task<OutboundMail> ComposeAsync(ReservationUnmatchedPaymentMail message,
        CancellationToken cancellationToken)
    {
        var payment = await database.ReservationDepositPayments.AsNoTracking()
            .SingleAsync(item => item.RequestId == message.RequestId, cancellationToken);
        var request = await database.ReservationDepositRequests.AsNoTracking()
            .SingleAsync(item => item.Id == message.RequestId, cancellationToken);
        var vehicle = await database.Vehicles.AsNoTracking()
            .SingleAsync(item => item.Id == request.VehicleId, cancellationToken);
        var email = await database.Users.AsNoTracking().Where(user => user.Id == request.CustomerId)
            .Select(user => user.Email).SingleAsync(cancellationToken);
        return ReservationUnmatchedPaymentMailComposer.Compose(payment, vehicle, email!, links.Absolute("/account"));
    }
}

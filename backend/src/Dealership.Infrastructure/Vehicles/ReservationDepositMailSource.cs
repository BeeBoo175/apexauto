using Dealership.Infrastructure.Mail;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Vehicles;

public sealed class ReservationDepositMailSource(DealershipDbContext database, ReservationDepositMailComposer composer,
    TimeProvider clock) : IMailDeliverySource
{
    public Task<ClaimedMail?> ClaimNextAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        // A request that was confirmed, cancelled or lapsed before delivery no longer needs its mail.
        var candidates = database.ReservationDepositMailOutbox.FromSqlInterpolated($"""
            SELECT m.* FROM "ReservationDepositMailOutbox" m
            -- The pending rule is ReservationDepositRequest.PendingAt; raw SQL cannot compose that expression.
            JOIN "ReservationDepositRequests" r ON r."Id" = m."RequestId"
            WHERE m."DeliveredAt" IS NULL AND m."NextDeliveryAt" <= {now}
              AND (m."DeliveryClaimExpiresAt" IS NULL OR m."DeliveryClaimExpiresAt" <= {now})
              AND r."ConfirmedAt" IS NULL AND r."CancelledAt" IS NULL AND r."ExpiresAt" > {now}
            ORDER BY m."NextDeliveryAt", m."Id" LIMIT 1 FOR UPDATE OF m SKIP LOCKED
            """);

        return MailDeliveryBookkeeping.ClaimNextAsync(database, candidates, now, ComposeAsync, cancellationToken);
    }

    public Task MarkDeliveredAsync(Guid messageId, Guid claimId, CancellationToken cancellationToken)
    {
        return MailDeliveryBookkeeping.MarkDeliveredAsync(database.ReservationDepositMailOutbox, messageId, claimId,
            clock.GetUtcNow(), cancellationToken);
    }

    private async Task<OutboundMail> ComposeAsync(ReservationDepositMail message, CancellationToken cancellationToken)
    {
        var request = await database.ReservationDepositRequests.AsNoTracking()
            .SingleAsync(item => item.Id == message.RequestId, cancellationToken);
        var branch = await database.Branches.AsNoTracking()
            .SingleAsync(item => item.Id == request.BranchId, cancellationToken);
        var vehicle = await database.Vehicles.AsNoTracking()
            .SingleAsync(item => item.Id == request.VehicleId, cancellationToken);
        return composer.Compose(message, request, branch.Name, vehicle);
    }
}

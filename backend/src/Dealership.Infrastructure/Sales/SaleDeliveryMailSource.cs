using Dealership.Application.Sales;
using Dealership.Infrastructure.Mail;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Sales;

public sealed class SaleDeliveryMailSource(DealershipDbContext database, TimeProvider clock,
    SaleDeliveryMailComposer composer) : IMailDeliverySource
{
    public Task<ClaimedMail?> ClaimNextAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var candidates = database.SaleDeliveryMailOutbox.FromSqlInterpolated($"""
            SELECT * FROM "SaleDeliveryMailOutbox"
            WHERE "DeliveredAt" IS NULL AND "NextDeliveryAt" <= {now}
              AND ("DeliveryClaimExpiresAt" IS NULL OR "DeliveryClaimExpiresAt" <= {now})
            ORDER BY "NextDeliveryAt", "Id" LIMIT 1 FOR UPDATE SKIP LOCKED
            """);
        return MailDeliveryBookkeeping.ClaimNextAsync(database, candidates, now, ComposeAsync, cancellationToken);
    }

    public Task MarkDeliveredAsync(Guid messageId, Guid claimId, CancellationToken cancellationToken)
    {
        return MailDeliveryBookkeeping.MarkDeliveredAsync(database.SaleDeliveryMailOutbox, messageId, claimId,
            clock.GetUtcNow(), cancellationToken);
    }

    private async Task<OutboundMail> ComposeAsync(SaleDeliveryMail message, CancellationToken cancellationToken)
    {
        var document = await database.SaleDocuments.AsNoTracking().SingleAsync(item =>
            item.SalesOrderId == message.SalesOrderId && item.Kind == SaleDocumentKind.HandoverNote, cancellationToken);
        return composer.Confirmation(message, document);
    }
}

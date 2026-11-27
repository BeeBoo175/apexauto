using Dealership.Application.Sales;
using Dealership.Domain.Sales;
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
            WHERE "DeliveredAt" IS NULL AND "RecipientEmail" IS NOT NULL AND "NextDeliveryAt" <= {now}
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
        var cause = await database.SalesOrderEvents.AsNoTracking()
            .Where(item => item.Id == message.DeliveryOperationId).Select(item => item.Kind)
            .SingleAsync(cancellationToken);
        var settled = cause == SalesOrderEventKind.Settled;
        var kind = settled ? SaleDocumentKind.PaymentReceipt : SaleDocumentKind.HandoverNote;
        var document = await database.SaleDocuments.AsNoTracking().SingleAsync(item =>
            item.SalesOrderId == message.SalesOrderId && item.Kind == kind &&
            item.CorrectionOperationId == null, cancellationToken);
        return settled ? composer.PaymentConfirmed(message, document) : composer.Confirmation(message, document);
    }
}

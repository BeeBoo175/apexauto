using Dealership.Infrastructure.Mail;

namespace Dealership.Infrastructure.Sales;

public sealed class SaleDeliveryMail : IMailDeliveryState
{
    private SaleDeliveryMail()
    {
    }

    public SaleDeliveryMail(Guid saleId, Guid operationId, string recipientEmail, DateTimeOffset dueAt)
    {
        Id = Guid.NewGuid();
        SalesOrderId = saleId;
        DeliveryOperationId = operationId;
        RecipientEmail = recipientEmail;
        NextDeliveryAt = dueAt;
    }

    public Guid Id { get; private set; }

    public Guid SalesOrderId { get; private set; }

    public Guid DeliveryOperationId { get; private set; }

    public string? RecipientEmail { get; private set; }

    public int DeliveryAttempts { get; set; }

    public DateTimeOffset NextDeliveryAt { get; set; }

    public Guid? DeliveryClaimId { get; set; }

    public DateTimeOffset? DeliveryClaimExpiresAt { get; set; }

    public DateTimeOffset? DeliveredAt { get; set; }
}

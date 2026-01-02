namespace Dealership.Infrastructure.Mail;

public interface IMailDeliveryState
{
    Guid Id { get; }

    int DeliveryAttempts { get; set; }

    DateTimeOffset NextDeliveryAt { get; set; }

    Guid? DeliveryClaimId { get; set; }

    DateTimeOffset? DeliveryClaimExpiresAt { get; set; }

    DateTimeOffset? DeliveredAt { get; set; }
}

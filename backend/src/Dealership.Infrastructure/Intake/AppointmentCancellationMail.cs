using Dealership.Infrastructure.Mail;

namespace Dealership.Infrastructure.Intake;

public sealed class AppointmentCancellationMail : IMailDeliveryState
{
    public Guid Id { get; init; }
    public string RecipientEmail { get; init; } = string.Empty;
    public int DeliveryAttempts { get; set; }
    public DateTimeOffset NextDeliveryAt { get; set; }
    public Guid? DeliveryClaimId { get; set; }
    public DateTimeOffset? DeliveryClaimExpiresAt { get; set; }
    public DateTimeOffset? DeliveredAt { get; set; }
}

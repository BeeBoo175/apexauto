using Dealership.Infrastructure.Mail;

namespace Dealership.Infrastructure.Identity;

public sealed class PasswordResetChallenge : IMailDeliveryState
{
    public Guid Id { get; init; }

    public Guid UserId { get; init; }

    public string? RecipientEmail { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset ExpiresAt { get; init; }

    public DateTimeOffset? InvalidatedAt { get; set; }

    public DateTimeOffset? ConsumedAt { get; set; }

    public string? CompletedSecurityStamp { get; set; }

    public int DeliveryAttempts { get; set; }

    public DateTimeOffset NextDeliveryAt { get; set; }

    public Guid? DeliveryClaimId { get; set; }

    public DateTimeOffset? DeliveryClaimExpiresAt { get; set; }

    public DateTimeOffset? DeliveredAt { get; set; }
}

using Dealership.Application.Accounts;
using Dealership.Infrastructure.Mail;

namespace Dealership.Infrastructure.Identity;

public sealed class EmailVerificationChallenge : IMailDeliveryState
{
    public static EmailVerificationChallenge Create(AccountUser user, Guid? resendKey, DateTimeOffset now)
    {
        return new EmailVerificationChallenge
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            RecipientEmail = user.Email!,
            CreatedAt = now,
            ExpiresAt = now + EmailVerificationPolicy.Lifetime,
            NextDeliveryAt = now,
            ResendKey = resendKey
        };
    }

    public Guid Id { get; init; }

    public Guid UserId { get; init; }

    public string RecipientEmail { get; init; } = string.Empty;

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset ExpiresAt { get; init; }

    public DateTimeOffset? InvalidatedAt { get; set; }

    public DateTimeOffset? ConsumedAt { get; set; }

    public int DeliveryAttempts { get; set; }

    public DateTimeOffset NextDeliveryAt { get; set; }

    public Guid? DeliveryClaimId { get; set; }

    public DateTimeOffset? DeliveryClaimExpiresAt { get; set; }

    public DateTimeOffset? DeliveredAt { get; set; }

    public Guid? ResendKey { get; init; }
}

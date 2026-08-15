using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Mail;

namespace Dealership.Infrastructure.Vehicles;

public sealed class ReservationDepositMail : IMailDeliveryState
{
    private ReservationDepositMail()
    {
    }

    public ReservationDepositMail(ReservationDepositRequest request, string recipientEmail)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(recipientEmail) || recipientEmail.Length > 256)
        {
            throw new ArgumentException("A deposit request mail requires a recipient.");
        }

        Id = Guid.NewGuid();
        RequestId = request.Id;
        RecipientEmail = recipientEmail;
        NextDeliveryAt = request.CreatedAt;
    }

    public Guid Id { get; private set; }

    public Guid RequestId { get; private set; }

    public string RecipientEmail { get; private set; } = string.Empty;

    public int DeliveryAttempts { get; set; }

    public DateTimeOffset NextDeliveryAt { get; set; }

    public Guid? DeliveryClaimId { get; set; }

    public DateTimeOffset? DeliveryClaimExpiresAt { get; set; }

    public DateTimeOffset? DeliveredAt { get; set; }
}

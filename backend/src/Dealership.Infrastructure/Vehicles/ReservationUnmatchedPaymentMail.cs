using Dealership.Infrastructure.Mail;

namespace Dealership.Infrastructure.Vehicles;

// The notice that a deposit was paid but no reservation could be made, so the customer knows to claim the refund. One
// unmatched payment has exactly one notice.
public sealed class ReservationUnmatchedPaymentMail : IMailDeliveryState
{
    private ReservationUnmatchedPaymentMail()
    {
    }

    public ReservationUnmatchedPaymentMail(Guid requestId, DateTimeOffset paidAt)
    {
        if (requestId == Guid.Empty || paidAt.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("A notice requires its payment request and UTC time.");
        }

        Id = Guid.NewGuid();
        RequestId = requestId;
        NextDeliveryAt = paidAt;
    }

    public Guid Id { get; private set; }

    public Guid RequestId { get; private set; }

    public int DeliveryAttempts { get; set; }

    public DateTimeOffset NextDeliveryAt { get; set; }

    public Guid? DeliveryClaimId { get; set; }

    public DateTimeOffset? DeliveryClaimExpiresAt { get; set; }

    public DateTimeOffset? DeliveredAt { get; set; }
}

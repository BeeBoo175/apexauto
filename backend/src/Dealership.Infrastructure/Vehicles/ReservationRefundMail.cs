using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Mail;

namespace Dealership.Infrastructure.Vehicles;

// The notice that a deposit refund reached the customer's bank. One completed refund has exactly one notice.
public sealed class ReservationRefundMail : IMailDeliveryState
{
    private ReservationRefundMail()
    {
    }

    public ReservationRefundMail(ReservationRefund refund)
    {
        ArgumentNullException.ThrowIfNull(refund);
        if (refund.State != RefundState.Completed || refund.SettledAt is null)
        {
            throw new ArgumentException("Only a completed refund has a notice.");
        }

        Id = Guid.NewGuid();
        RefundId = refund.PaymentRequestId;
        NextDeliveryAt = refund.SettledAt.Value;
    }

    public Guid Id { get; private set; }

    public Guid RefundId { get; private set; }

    public int DeliveryAttempts { get; set; }

    public DateTimeOffset NextDeliveryAt { get; set; }

    public Guid? DeliveryClaimId { get; set; }

    public DateTimeOffset? DeliveryClaimExpiresAt { get; set; }

    public DateTimeOffset? DeliveredAt { get; set; }
}

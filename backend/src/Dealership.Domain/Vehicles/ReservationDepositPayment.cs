namespace Dealership.Domain.Vehicles;

public enum DepositPaymentState
{
    // The bill exists and the customer has not been confirmed as paying.
    Pending,

    // The gateway confirmed the full deposit and the reservation was made.
    Paid,

    // The gateway took money but no reservation could be made, so staff refund it by hand.
    PaidUnmatched
}

// One hosted payment for the deposit of a staff deposit request. The request id is the key, so a request has one
// bill however many times the customer opens the link.
public sealed class ReservationDepositPayment
{
    private ReservationDepositPayment()
    {
    }

    public ReservationDepositPayment(Guid requestId, string billId, string billUrl, int amountSen,
        DateTimeOffset now)
    {
        if (requestId == Guid.Empty || string.IsNullOrWhiteSpace(billId) || billId.Length > 64 ||
            string.IsNullOrWhiteSpace(billUrl) || billUrl.Length > 512 || amountSen <= 0 ||
            now.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("A deposit payment requires its request, bill, positive amount and UTC time.");
        }

        RequestId = requestId;
        BillId = billId;
        BillUrl = billUrl;
        AmountSen = amountSen;
        CreatedAt = now;
        State = DepositPaymentState.Pending;
    }

    public Guid RequestId { get; private set; }
    public string BillId { get; private set; } = string.Empty;
    public string BillUrl { get; private set; } = string.Empty;
    public int AmountSen { get; private set; }
    public DepositPaymentState State { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? PaidAt { get; private set; }
    public int? PaidAmountSen { get; private set; }

    // Idempotent: only a pending payment changes, so a repeated gateway callback records nothing new.
    public bool RecordPaid(int paidAmountSen, bool reserved, DateTimeOffset now)
    {
        if (State != DepositPaymentState.Pending)
        {
            return false;
        }

        if (paidAmountSen < 0 || now.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("A payment records a non-negative amount and UTC time.");
        }

        PaidAt = now;
        PaidAmountSen = paidAmountSen;
        State = reserved && paidAmountSen == AmountSen ? DepositPaymentState.Paid : DepositPaymentState.PaidUnmatched;
        return true;
    }
}

using Dealership.Domain.Vehicles;

namespace Dealership.Application.Vehicles;

public sealed record PayoutRequest(string Reference, string BankCode, string AccountNumber, string AccountHolder,
    int AmountSen, string Description);

// Where a payout stands, whatever the provider calls its statuses.
public enum PayoutOutcome
{
    Processing,
    Completed,

    // The provider returned or cancelled the money.
    Failed
}

public sealed record PayoutResult(string Id, PayoutOutcome Outcome);

public sealed record PayoutNotice(string PayoutId, PayoutOutcome Outcome);

// The provider's money-out side. Refunds are real only when it is enabled; otherwise the deposit stays a simulated
// record and no refund is offered.
public interface IPayoutGateway
{
    bool IsEnabled { get; }

    Task<PayoutResult> CreatePayoutAsync(PayoutRequest request, CancellationToken cancellationToken);

    // The payout's current outcome, for a payout whose callback has not arrived.
    Task<PayoutOutcome> GetPayoutOutcomeAsync(string payoutId, CancellationToken cancellationToken);

    // The provider's callback with its checksum checked and its field names translated; null when the checksum is
    // invalid or the message is unreadable.
    PayoutNotice? ReadNotice(IReadOnlyDictionary<string, string> fields);
}

public enum RefundDueReason
{
    // The reservation the deposit paid for was released.
    ReservationReleased,

    // The deposit was paid but no reservation could be made.
    PaymentUnmatched
}

public sealed record RefundView(Guid PaymentRequestId, int AmountSen, RefundDueReason Reason, string Vehicle,
    string State, string? BankCode, string? AccountLast4, DateTimeOffset DueAt);

public interface IReservationRefundStore
{
    Task<IReadOnlyList<RefundView>> ListAsync(Guid customerId, CancellationToken cancellationToken);

    // The deposit a customer may be refunded now, or null when it is not theirs or not yet refundable.
    Task<RefundDue?> FindDueAsync(Guid customerId, Guid paymentRequestId, CancellationToken cancellationToken);

    Task<ReservationRefund?> FindAsync(Guid paymentRequestId, CancellationToken cancellationToken);

    Task<ReservationRefund?> FindByPayoutAsync(string payoutId, CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> ProcessingPayoutsAsync(Guid customerId, CancellationToken cancellationToken);

    // False when a parallel request already holds the refund, which the caller then reads instead.
    Task<bool> AddAsync(ReservationRefund refund, CancellationToken cancellationToken);

    Task SaveAsync(ReservationRefund refund, CancellationToken cancellationToken);
}

public sealed record RefundDue(Guid PaymentRequestId, Guid CustomerId, int AmountSen, RefundDueReason Reason);

public sealed record BankAccountDetails(string? BankCode, string? AccountNumber, string? AccountHolder);

public enum RefundStatus
{
    Processing,
    Completed,
    Invalid,
    NotFound,
    Unavailable
}

public sealed record RefundResult(RefundStatus Status, RefundView? Refund = null);

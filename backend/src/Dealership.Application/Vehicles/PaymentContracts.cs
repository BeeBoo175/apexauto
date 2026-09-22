using Dealership.Domain.Vehicles;

namespace Dealership.Application.Vehicles;

public sealed record PaymentBillRequest(Guid RequestId, string PayerName, string PayerEmail, int AmountSen,
    string Description);

public sealed record PaymentBill(string Id, string Url);

public class PaymentGatewayException(string message, Exception? inner = null) : Exception(message, inner);

// The request may have reached the provider before the wait ended, so whether it took effect is not known. Callers
// that move money must not treat it as a refusal.
public sealed class PaymentGatewayTimeoutException(Exception? inner = null)
    : PaymentGatewayException("The payment provider did not answer in time.", inner);

// What a verified provider message says about one bill, in the application's own terms. A null amount means the
// message states the bill was paid in full without naming the sum.
// PaidAt is the provider's own payment time, so a notice that arrives late still shows when the customer paid.
public sealed record PaymentNotice(string BillId, bool Paid, int? PaidAmountSen, DateTimeOffset? PaidAt);

// The addresses the provider's server and the customer's browser send results to. They name no provider so one
// registration works for whichever gateway is configured.
public static class PaymentRoutes
{
    public const string Callback = "/api/payments/callback";
    public const string Return = "/api/payments/returns";
    public const string Mode = "/api/payments/mode";
    public const string PayoutCallback = "/api/payments/payouts/callback";
}

// The hosted payment provider. When it is not enabled the staff deposit link keeps confirming the simulated deposit.
// A new provider implements this port and the payout port, and is selected where the gateways are registered.
public interface IPaymentGateway
{
    bool IsEnabled { get; }

    Task<PaymentBill> CreateBillAsync(PaymentBillRequest request, CancellationToken cancellationToken);

    // Removes an unpaid bill so it cannot be paid. True only when the provider confirmed the bill is gone; false
    // when the provider refused or could not be reached, so the bill may still be payable.
    Task<bool> DeleteBillAsync(string billId, CancellationToken cancellationToken);

    // What the provider currently says about a bill, read by asking it. Null when it could not be reached, so a null
    // answer must never be taken as unpaid.
    Task<PaymentNotice?> FindBillAsync(string billId, CancellationToken cancellationToken);

    // The provider's message, from its server callback or the customer's signed return, with the signature checked
    // and the provider's field names translated; null when the signature is invalid or the message is unreadable.
    PaymentNotice? ReadNotice(IReadOnlyDictionary<string, string> fields);
}

public sealed record DepositPayer(string Name, string Email);

public enum DepositPaymentAddResult
{
    Added,

    // The request already has a payment, which the caller then reads instead.
    Exists,

    // The request no longer holds its car or its customer closed the account, so no bill may be kept.
    Refused
}

public interface IReservationPaymentStore
{
    Task<ReservationDepositPayment?> FindByRequestAsync(Guid requestId, CancellationToken cancellationToken);

    Task<ReservationDepositPayment?> FindByBillAsync(string billId, CancellationToken cancellationToken);

    Task<DepositPayer?> FindPayerAsync(Guid customerId, CancellationToken cancellationToken);

    // Stores the payment under a lock on its request, rechecking that the request still holds its car and its customer
    // is not closed, using the database clock.
    Task<DepositPaymentAddResult> AddAsync(ReservationDepositPayment payment, CancellationToken cancellationToken);

    // Records that the provider confirmed the unpaid bill of a still pending payment is gone.
    Task MarkBillDeletedAsync(Guid requestId, DateTimeOffset now, CancellationToken cancellationToken);

    // Requests that stopped holding their car since the given time (cancelled, or lapsed unconfirmed) and still have an
    // unpaid bill.
    Task<IReadOnlyList<Guid>> ReleasableRequestsAsync(DateTimeOffset since, DateTimeOffset now, int limit,
        CancellationToken cancellationToken);

    // Persists the outcome only while the payment is still pending; false when another delivery recorded it first.
    Task<bool> RecordPaidAsync(string billId, int paidAmountSen, bool reserved, DateTimeOffset paidAt,
        CancellationToken cancellationToken);
}

public enum DepositPaymentStatus
{
    // The deposit is paid and the reservation exists.
    Confirmed,

    // The customer must pay at the returned address.
    PaymentRequired,

    // The customer paid but the callback has not arrived yet.
    Waiting,

    Invalid,
    Lapsed,
    Conflict,
    Unavailable
}

public sealed record DepositPaymentResult(DepositPaymentStatus Status, string? PaymentUrl = null);

using Dealership.Domain.Vehicles;

namespace Dealership.Application.Vehicles;

public sealed record PaymentBillRequest(Guid RequestId, string PayerName, string PayerEmail, int AmountSen,
    string Description);

public sealed record PaymentBill(string Id, string Url);

public sealed class PaymentGatewayException(string message, Exception? inner = null) : Exception(message, inner);

// What a verified provider message says about one bill, in the application's own terms. A null amount means the
// message states the bill was paid in full without naming the sum.
public sealed record PaymentNotice(string BillId, bool Paid, int? PaidAmountSen);

// The addresses the provider's server and the customer's browser send results to. They name no provider so one
// registration works for whichever gateway is configured.
public static class PaymentRoutes
{
    public const string Callback = "/api/payments/callback";
    public const string Return = "/api/payments/returns";
    public const string PayoutCallback = "/api/payments/payouts/callback";
}

// The hosted payment provider. When it is not enabled the staff deposit link keeps confirming the simulated deposit.
// A new provider implements this port and the payout port, and is selected where the gateways are registered.
public interface IPaymentGateway
{
    bool IsEnabled { get; }

    Task<PaymentBill> CreateBillAsync(PaymentBillRequest request, CancellationToken cancellationToken);

    // Best effort: removes a bill that lost a creation race so it cannot be paid twice.
    Task DeleteBillAsync(string billId, CancellationToken cancellationToken);

    // The provider's message, from its server callback or the customer's signed return, with the signature checked
    // and the provider's field names translated; null when the signature is invalid or the message is unreadable.
    PaymentNotice? ReadNotice(IReadOnlyDictionary<string, string> fields);
}

public sealed record DepositPayer(string Name, string Email);

public interface IReservationPaymentStore
{
    Task<ReservationDepositPayment?> FindByRequestAsync(Guid requestId, CancellationToken cancellationToken);

    Task<ReservationDepositPayment?> FindByBillAsync(string billId, CancellationToken cancellationToken);

    Task<DepositPayer?> FindPayerAsync(Guid customerId, CancellationToken cancellationToken);

    // False when the request already has a payment, which the caller then reads instead.
    Task<bool> AddAsync(ReservationDepositPayment payment, CancellationToken cancellationToken);

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

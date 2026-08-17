using Dealership.Domain.Vehicles;

namespace Dealership.Application.Vehicles;

public sealed record PaymentBillRequest(Guid RequestId, string PayerName, string PayerEmail, int AmountSen,
    string Description);

public sealed record PaymentBill(string Id, string Url);

public sealed class PaymentGatewayException(string message, Exception? inner = null) : Exception(message, inner);

// The hosted payment provider. When it is not enabled the staff deposit link keeps confirming the simulated deposit.
public interface IPaymentGateway
{
    bool IsEnabled { get; }

    Task<PaymentBill> CreateBillAsync(PaymentBillRequest request, CancellationToken cancellationToken);

    // Best effort: removes a bill that lost a creation race so it cannot be paid twice.
    Task DeleteBillAsync(string billId, CancellationToken cancellationToken);

    // True only when the provider's signature over the fields is valid.
    bool VerifyCallback(IReadOnlyDictionary<string, string> fields);
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

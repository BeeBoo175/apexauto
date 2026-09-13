namespace Dealership.Domain.Vehicles;

public enum RefundState
{
    // The payout was asked for and the bank has not confirmed it.
    Processing,

    Completed,

    // The payout could not be made or was returned, so the customer may give their bank details again.
    Failed
}

// The payout of a paid deposit back to its customer. A paid deposit is refundable once its reservation was released,
// or at once when it was paid but could not be matched to a reservation. The account number is sent to the provider
// and never stored; only the bank, the last four digits and the holder's name stay for the customer's own view.
public sealed class ReservationRefund
{
    // Long enough for the provider sandbox's DUMMYBANKVERIFIED as well as the SWIFT codes of real banks.
    public const int MaximumBankCodeLength = 24;
    public const int AccountHolderMaximumLength = 100;
    public const int AccountNumberMinimumLength = 6;
    public const int AccountNumberMaximumLength = 20;

    private ReservationRefund()
    {
    }

    public ReservationRefund(Guid paymentRequestId, Guid customerId, int amountSen, string bankCode,
        string accountNumber, string accountHolder, DateTimeOffset now)
    {
        if (paymentRequestId == Guid.Empty || customerId == Guid.Empty || amountSen <= 0 ||
            now.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("A refund requires its payment, customer, positive amount and UTC time.");
        }

        PaymentRequestId = paymentRequestId;
        CustomerId = customerId;
        AmountSen = amountSen;
        RequestedAt = now;
        Attempt = 1;
        State = RefundState.Processing;
        SetAccount(bankCode, accountNumber, accountHolder);
    }

    public Guid PaymentRequestId { get; private set; }
    public Guid CustomerId { get; private set; }
    public int AmountSen { get; private set; }
    public RefundState State { get; private set; }
    public int Attempt { get; private set; }
    public string BankCode { get; private set; } = string.Empty;
    public string AccountLast4 { get; private set; } = string.Empty;
    // NULL once the retention executor has removed the holder name of a completed refund.
    public string? AccountHolder { get; private set; } = string.Empty;
    public string? PayoutId { get; private set; }
    public DateTimeOffset RequestedAt { get; private set; }
    public DateTimeOffset? SettledAt { get; private set; }

    // The reference the provider keeps for this attempt; a new attempt gets a new reference.
    public string Reference => $"{PaymentRequestId:N}-{Attempt}";

    // The payment and attempt a provider reference names, for a notice that arrives without a saved payout id.
    public static bool TryParseReference(string? reference, out Guid paymentRequestId, out int attempt)
    {
        paymentRequestId = Guid.Empty;
        attempt = 0;
        var parts = reference?.Split('-');
        return parts is { Length: 2 } && Guid.TryParseExact(parts[0], "N", out paymentRequestId) &&
            int.TryParse(parts[1], out attempt) && attempt > 0;
    }

    public static bool IsValidAccountHolder(string? accountHolder) =>
        accountHolder?.Trim() is { Length: > 0 and <= AccountHolderMaximumLength } holder && !holder.Any(char.IsControl);

    public static bool IsValidAccountNumber(string? accountNumber) => accountNumber is not null &&
        accountNumber.Length is >= AccountNumberMinimumLength and <= AccountNumberMaximumLength &&
        accountNumber.All(char.IsAsciiDigit);

    public void AttachPayout(string payoutId)
    {
        if (State != RefundState.Processing || string.IsNullOrWhiteSpace(payoutId) || payoutId.Length > 64)
        {
            throw new InvalidOperationException("Only a processing refund takes its payout reference.");
        }

        PayoutId = payoutId;
    }

    // Only a failed refund starts again, with a fresh attempt number so the provider sees a new reference.
    public bool Retry(string bankCode, string accountNumber, string accountHolder, DateTimeOffset now)
    {
        if (State != RefundState.Failed)
        {
            return false;
        }

        Attempt++;
        State = RefundState.Processing;
        PayoutId = null;
        SettledAt = null;
        RequestedAt = now;
        SetAccount(bankCode, accountNumber, accountHolder);
        return true;
    }

    // Idempotent: only a processing refund settles, so a repeated callback records nothing new.
    public bool Settle(bool completed, DateTimeOffset now)
    {
        if (State != RefundState.Processing)
        {
            return false;
        }

        State = completed ? RefundState.Completed : RefundState.Failed;
        SettledAt = now;
        return true;
    }

    // The attempt failed before the provider took it, so it is not left waiting for a callback.
    public bool Fail(DateTimeOffset now) => Settle(false, now);

    private void SetAccount(string bankCode, string accountNumber, string accountHolder)
    {
        var holder = accountHolder?.Trim();
        if (string.IsNullOrWhiteSpace(bankCode) || bankCode.Length > MaximumBankCodeLength || !bankCode.All(char.IsAsciiLetterOrDigit) ||
            !IsValidAccountNumber(accountNumber) || !IsValidAccountHolder(holder))
        {
            throw new ArgumentException("A refund requires a bank code, a numeric account number and the account holder.");
        }

        BankCode = bankCode;
        AccountLast4 = accountNumber[^4..];
        AccountHolder = holder!;
    }
}

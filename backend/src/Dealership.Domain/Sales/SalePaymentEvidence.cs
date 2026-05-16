namespace Dealership.Domain.Sales;

public enum SalePaymentMethod
{
    Cash,
    BankTransfer,
    HirePurchase,
    ZeroBalance
}

// Manual evidence of payment outside ApexAuto; the balance already includes deposit and trade-in credits.
public sealed record SalePaymentEvidence(SalePaymentMethod Method, decimal ReceivedAmount,
    string? PaymentReference, DateOnly? PaymentDate, string? Bank, decimal FinancingAmount,
    string? LouReference, bool LouVerified, bool ZeroBalanceConfirmed)
{
    public const int ReferenceMaximumLength = 1000;
    public const int BankMaximumLength = 100;

    public SalePaymentEvidence Normalize(decimal balancePayable)
    {
        SalePricing.EnsureAmount(balancePayable);
        SalePricing.EnsureAmount(ReceivedAmount);
        SalePricing.EnsureAmount(FinancingAmount);
        var evidence = this with
        {
            PaymentReference = OptionalText(PaymentReference, ReferenceMaximumLength),
            Bank = OptionalText(Bank, BankMaximumLength),
            LouReference = OptionalText(LouReference, ReferenceMaximumLength)
        };

        if (balancePayable == 0m)
        {
            evidence.RequireZeroBalance();
            return evidence;
        }

        if (ZeroBalanceConfirmed || Method == SalePaymentMethod.ZeroBalance)
        {
            throw new ArgumentException("Zero-balance confirmation requires a zero approved balance.");
        }

        switch (Method)
        {
            case SalePaymentMethod.Cash:
            case SalePaymentMethod.BankTransfer:
                evidence.RequireReceivedPayment(balancePayable);
                break;
            case SalePaymentMethod.HirePurchase:
                evidence.RequireHirePurchase(balancePayable);
                break;
            default:
                throw new ArgumentException("Select a supported settlement method.");
        }

        return evidence;
    }

    private void RequireZeroBalance()
    {
        if (Method != SalePaymentMethod.ZeroBalance || !ZeroBalanceConfirmed || ReceivedAmount != 0m ||
            FinancingAmount != 0m || PaymentReference is not null || PaymentDate is not null ||
            Bank is not null || LouReference is not null || LouVerified)
        {
            throw new ArgumentException("A zero balance needs explicit confirmation without payment evidence.");
        }
    }

    private void RequireReceivedPayment(decimal balancePayable)
    {
        if (ReceivedAmount != balancePayable || PaymentReference is null || PaymentDate is null ||
            FinancingAmount != 0m || Bank is not null || LouReference is not null || LouVerified)
        {
            throw new ArgumentException("Cash or transfer requires the exact balance, a payment reference and date.");
        }
    }

    private void RequireHirePurchase(decimal balancePayable)
    {
        if (FinancingAmount <= 0m || FinancingAmount + ReceivedAmount != balancePayable ||
            Bank is null || LouReference is null || !LouVerified || PaymentReference is not null ||
            PaymentDate is not null)
        {
            throw new ArgumentException("Hire purchase requires verified financing and received contribution covering the exact balance.");
        }
    }

    private static string? OptionalText(string? input, int maximumLength)
    {
        var value = input?.Trim();
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        if (value.Length > maximumLength || value.Any(char.IsControl))
        {
            throw new ArgumentException("Payment evidence must be bounded text without control characters.");
        }

        return value;
    }
}

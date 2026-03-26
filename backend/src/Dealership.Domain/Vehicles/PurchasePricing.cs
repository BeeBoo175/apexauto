using Dealership.Domain.Common;

namespace Dealership.Domain.Vehicles;

public sealed record PurchasePrice(decimal VehiclePrice, decimal ProcessingFee, decimal RoadTax,
    decimal ServiceTax, decimal PromoDiscount, decimal Total);

public sealed record FinancingEstimate(decimal Principal, decimal AnnualRate, int Months,
    decimal Interest, decimal TotalRepayments, decimal MonthlyInstallment, decimal FinalInstallment);

public static class PurchasePricing
{
    public const decimal AnnualFlatRate = 0.03m;
    public const int DefaultTermYears = 7;
    public const decimal DefaultCashContributionRate = 0.10m;

    public static PurchasePrice CalculatePrice(decimal vehiclePrice, decimal processingFee,
        decimal roadTax, decimal serviceTaxRate, decimal promoDiscount = 0)
    {
        EnsureAmount(vehiclePrice, nameof(vehiclePrice));
        EnsureAmount(processingFee, nameof(processingFee));
        EnsureAmount(roadTax, nameof(roadTax));
        EnsureAmount(promoDiscount, nameof(promoDiscount));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(vehiclePrice);
        ArgumentOutOfRangeException.ThrowIfNegative(serviceTaxRate);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(serviceTaxRate, 1m);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(promoDiscount, vehiclePrice);

        var serviceTax = Money.From(processingFee * serviceTaxRate).Amount;
        var total = vehiclePrice - promoDiscount + processingFee + roadTax + serviceTax;
        return new PurchasePrice(vehiclePrice, processingFee, roadTax, serviceTax, promoDiscount, total);
    }

    public static FinancingEstimate CalculateFinancing(decimal total, decimal cashContribution,
        decimal indicativeTradeIn, int termYears = DefaultTermYears)
    {
        EnsureAmount(total, nameof(total));
        EnsureAmount(cashContribution, nameof(cashContribution));
        EnsureAmount(indicativeTradeIn, nameof(indicativeTradeIn));
        ArgumentOutOfRangeException.ThrowIfLessThan(termYears, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(termYears, 9);

        // Cash contribution already includes any paid reservation deposit.
        var remaining = Math.Max(0, total - cashContribution);
        var principal = Math.Max(0, remaining - indicativeTradeIn);
        var interest = Money.From(principal * AnnualFlatRate * termYears).Amount;
        var repayments = principal + interest;
        var months = termYears * 12;
        var installment = Money.From(repayments / months).Amount;

        // Tiny estimates must not accumulate rounded payments beyond the total.
        if (installment * (months - 1) > repayments)
        {
            installment = decimal.Floor(repayments / months * 100m) / 100m;
        }

        var finalInstallment = repayments - installment * (months - 1);
        return new FinancingEstimate(principal, AnnualFlatRate, months, interest,
            repayments, installment, finalInstallment);
    }

    private static void EnsureAmount(decimal amount, string parameterName)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount, parameterName);
        if (decimal.Round(amount, 2) != amount)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Use exact MYR minor units.");
        }
    }
}

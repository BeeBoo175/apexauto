using Dealership.Domain.Common;

namespace Dealership.Domain.Sales;

public sealed record SaleTerms(decimal BasePrice, decimal RoadTax, decimal ProcessingFee, decimal SstRate,
    decimal PromoDiscount, decimal NegotiatedDiscount, decimal WarrantyPrice, decimal DepositCredit,
    decimal TradeInAllowance);

public sealed record SalePrice(decimal ServiceTax, decimal Subtotal, decimal BalancePayable);

// Itemization: base - promo - negotiated + processing fee + road tax + SST (on the processing fee only)
// + warranty = subtotal; balance = subtotal - deposit credit - trade-in allowance.
public static class SalePricing
{
    public static SalePrice Calculate(SaleTerms terms)
    {
        foreach (var amount in new[]
        {
            terms.RoadTax, terms.ProcessingFee, terms.PromoDiscount, terms.NegotiatedDiscount, terms.WarrantyPrice,
            terms.DepositCredit, terms.TradeInAllowance
        })
        {
            Ensure(amount);
        }

        Ensure(terms.BasePrice);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(terms.BasePrice);
        ArgumentOutOfRangeException.ThrowIfNegative(terms.SstRate);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(terms.SstRate, 1m);
        if (terms.PromoDiscount + terms.NegotiatedDiscount > terms.BasePrice)
        {
            throw new ArgumentOutOfRangeException(nameof(terms), "Discounts cannot exceed the base price.");
        }

        var serviceTax = Money.FromProduct(terms.ProcessingFee, terms.SstRate).Amount;
        var subtotal = terms.BasePrice - terms.PromoDiscount - terms.NegotiatedDiscount + terms.ProcessingFee +
            terms.RoadTax + serviceTax + terms.WarrantyPrice;
        var balance = subtotal - terms.DepositCredit - terms.TradeInAllowance;
        if (balance < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(terms), "Deposit and trade-in credit cannot exceed the deal total.");
        }

        return new SalePrice(serviceTax, subtotal, balance);
    }

    private static void Ensure(decimal amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(amount, 9999999999999999.99m);
        if (decimal.Round(amount, 2) != amount)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Amounts must be exact sen values.");
        }
    }
}

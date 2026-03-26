using Dealership.Domain.Common;
using Dealership.Domain.Vehicles;

namespace Dealership.UnitTests;

public sealed class PurchasePricingTests
{
    [Fact]
    public void PriceRoundsTheExactTaxProductOnceAtMinorUnits()
    {
        const decimal rate = 0.1666666666666666666666666666m;
        Assert.Equal(0m, PurchasePricing.CalculatePrice(1m, 0.03m, 0m, rate).ServiceTax);
        Assert.Equal(0.01m, PurchasePricing.CalculatePrice(1m, 0.03m, 0m, rate + 0.0000000000000000000000000001m).ServiceTax);
        Assert.Equal(0.01m, Money.FromProduct(0.05m, 0.1m).Amount);
        Assert.Equal(-0.01m, Money.FromProduct(-0.05m, 0.1m).Amount);
        Assert.Equal(0m, Money.FromProduct(-0.03m, rate).Amount);
        Assert.Equal(decimal.MaxValue, Money.FromProduct(decimal.MaxValue, 1m).Amount);
    }

    [Fact]
    public void PriceTaxesOnlyTheProcessingFeeAndSubtractsOnePromo()
    {
        var price = PurchasePricing.CalculatePrice(60000m, 500m, 90m, 0.08m, 1000m);

        Assert.Equal(new PurchasePrice(60000m, 500m, 90m, 40m, 1000m, 59630m), price);
        Assert.Equal(630m, PurchasePricing.CalculatePrice(60000m, 500m, 90m, 0.08m, 60000m).Total);
        Assert.Equal(0.01m, PurchasePricing.CalculatePrice(1m, 0.05m, 0m, 0.10m).ServiceTax);
    }

    [Fact]
    public void ApprovedExampleReconcilesFlatInterestAndTheFinalInstallment()
    {
        var estimate = PurchasePricing.CalculateFinancing(60000m, 6000m, 9000m);

        Assert.Equal(new FinancingEstimate(45000m, 0.03m, 84, 9450m, 54450m, 648.21m, 648.57m), estimate);
        Assert.Equal(estimate.TotalRepayments,
            estimate.MonthlyInstallment * (estimate.Months - 1) + estimate.FinalInstallment);
    }

    [Theory]
    [InlineData(60000, 0)]
    [InlineData(0, 60000)]
    [InlineData(6000, 65000)]
    [InlineData(65000, 6000)]
    public void CoveredPurchaseNeedsNoFinancingOrSurplusWorkflow(decimal cash, decimal tradeIn)
    {
        var estimate = PurchasePricing.CalculateFinancing(60000m, cash, tradeIn);

        Assert.Equal(0m, estimate.Principal);
        Assert.Equal(0m, estimate.Interest);
        Assert.Equal(0m, estimate.TotalRepayments);
        Assert.Equal(0m, estimate.MonthlyInstallment);
        Assert.Equal(0m, estimate.FinalInstallment);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(9)]
    public void EvenTinyLoansHaveNonnegativeReconciledPayments(int years)
    {
        var estimate = PurchasePricing.CalculateFinancing(0.50m, 0m, 0m, years);

        Assert.True(estimate.MonthlyInstallment >= 0m);
        Assert.True(estimate.FinalInstallment >= 0m);
        Assert.Equal(estimate.TotalRepayments,
            estimate.MonthlyInstallment * (estimate.Months - 1) + estimate.FinalInstallment);
        Assert.Equal(years * 12, estimate.Months);
    }

    [Theory]
    [InlineData(0, 500, 90, 0.08, 0)]
    [InlineData(-1, 500, 90, 0.08, 0)]
    [InlineData(60000, -1, 90, 0.08, 0)]
    [InlineData(60000, 500, -1, 0.08, 0)]
    [InlineData(60000, 500, 90, -0.01, 0)]
    [InlineData(60000, 500, 90, 1.01, 0)]
    [InlineData(60000, 500, 90, 0.08, -1)]
    [InlineData(60000, 500, 90, 0.08, 60001)]
    [InlineData(60000.001, 500, 90, 0.08, 0)]
    [InlineData(60000, 500.001, 90, 0.08, 0)]
    [InlineData(60000, 500, 90.001, 0.08, 0)]
    [InlineData(60000, 500, 90, 0.08, 0.001)]
    public void InvalidPriceInputsCannotProduceAnEstimate(decimal price, decimal fee,
        decimal roadTax, decimal taxRate, decimal promo)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            PurchasePricing.CalculatePrice(price, fee, roadTax, taxRate, promo);
        });
    }

    [Theory]
    [InlineData(-1, 0, 0, 7)]
    [InlineData(60000, -1, 0, 7)]
    [InlineData(60000, 0, -1, 7)]
    [InlineData(60000.001, 0, 0, 7)]
    [InlineData(60000, 0.001, 0, 7)]
    [InlineData(60000, 0, 0.001, 7)]
    [InlineData(60000, 0, 0, 0)]
    [InlineData(60000, 0, 0, 10)]
    public void InvalidFinanceInputsCannotProduceAnEstimate(decimal total, decimal cash,
        decimal tradeIn, int years)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            PurchasePricing.CalculateFinancing(total, cash, tradeIn, years);
        });
    }
}

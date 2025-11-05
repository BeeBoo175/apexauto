using Dealership.Domain.Common;

namespace Dealership.Domain.Valuation;

public static class AlgorithmicValuationEngine
{
    public static ValuationQuote Calculate(decimal baselinePrice, int manufactureYear, int mileageKilometres, int currentYear)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(baselinePrice);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(manufactureYear);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(manufactureYear, currentYear);
        ArgumentOutOfRangeException.ThrowIfNegative(mileageKilometres);

        var age = currentYear - manufactureYear;
        var floor = baselinePrice * ValuationPolicy.SalvageFloorRate;
        var depreciatedPrice = baselinePrice;

        // Further depreciation cannot affect the quote once even the maximum premium reaches the floor.
        var depreciationCutoff = floor / (1m + ValuationPolicy.MaximumMileagePremium);

        for (var year = 1; year <= age && depreciatedPrice > depreciationCutoff; year++)
        {
            depreciatedPrice *= 1m - GetDepreciationRate(year);
        }

        var expectedMileage = (decimal)age * ValuationPolicy.AnnualMileageKilometres;
        var mileageDelta = mileageKilometres - expectedMileage;
        var adjustment = -mileageDelta / ValuationPolicy.MileageAdjustmentUnit * ValuationPolicy.MileageAdjustmentRate;
        adjustment = Math.Clamp(adjustment, -ValuationPolicy.MaximumMileagePenalty, ValuationPolicy.MaximumMileagePremium);

        var midpoint = Math.Max(floor, depreciatedPrice * (1m + adjustment));
        var minimum = Math.Max(floor, midpoint * (1m - ValuationPolicy.QuoteSpread));
        var maximum = Math.Max(floor, midpoint * (1m + ValuationPolicy.QuoteSpread));

        return new ValuationQuote(Money.From(minimum), Money.From(maximum));
    }

    private static decimal GetDepreciationRate(int year)
    {
        if (year == 1)
        {
            return ValuationPolicy.FirstYearDepreciation;
        }

        if (year <= ValuationPolicy.MiddleYearsEnd)
        {
            return ValuationPolicy.MiddleYearDepreciation;
        }

        return ValuationPolicy.LaterYearDepreciation;
    }
}

namespace Dealership.Domain.Valuation;

public static class ValuationPolicy
{
    public const decimal FirstYearDepreciation = 0.15m;
    public const decimal MiddleYearDepreciation = 0.09m;
    public const decimal LaterYearDepreciation = 0.06m;
    public const int MiddleYearsEnd = 5;
    public const int AnnualMileageKilometres = 15000;
    public const int MileageAdjustmentUnit = 1000;
    public const decimal MileageAdjustmentRate = 0.005m;
    public const decimal MaximumMileagePenalty = 0.20m;
    public const decimal MaximumMileagePremium = 0.10m;
    public const decimal SalvageFloorRate = 0.10m;
    public const decimal QuoteSpread = 0.05m;
    public const int MaximumMileageKilometres = 500000;
}

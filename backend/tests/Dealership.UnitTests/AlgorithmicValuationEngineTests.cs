using Dealership.Domain.Valuation;

namespace Dealership.UnitTests;

public class AlgorithmicValuationEngineTests
{
    [Theory]
    [InlineData(2026, 0, 95000, 105000)]
    [InlineData(2025, 15000, 80750, 89250)]
    [InlineData(2024, 30000, 73482.50, 81217.50)]
    [InlineData(2021, 75000, 55374.28, 61203.15)]
    [InlineData(2020, 90000, 52051.82, 57530.96)]
    [InlineData(2025, 16000, 80346.25, 88803.75)]
    [InlineData(2025, 15500, 80548.13, 89026.88)]
    [InlineData(2025, 0, 86806.25, 95943.75)]
    [InlineData(2024, 0, 80830.75, 89339.25)]
    [InlineData(2025, 1000000, 64600, 71400)]
    [InlineData(1900, 1000000, 10000, 10500)]
    public void CalculatesAgreedDepreciationMileageAndFloor(int year, int mileage, decimal minimum, decimal maximum)
    {
        var quote = AlgorithmicValuationEngine.Calculate(100000m, year, mileage, 2026);

        Assert.Equal(minimum, quote.MinValuation.Amount);
        Assert.Equal(maximum, quote.MaxValuation.Amount);
        Assert.Equal("MYR", quote.MinValuation.Currency);
    }

    [Theory]
    [InlineData(0, 2025, 15000)]
    [InlineData(-1, 2025, 15000)]
    [InlineData(100000, 0, 15000)]
    [InlineData(100000, 2027, 15000)]
    [InlineData(100000, 2025, -1)]
    public void RejectsInvalidInputs(decimal price, int year, int mileage)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            AlgorithmicValuationEngine.Calculate(price, year, mileage, 2026);
        });
    }
}

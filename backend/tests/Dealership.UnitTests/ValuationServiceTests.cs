using Dealership.Application.Vehicles;

namespace Dealership.UnitTests;

public sealed class ValuationServiceTests
{
    [Theory]
    [InlineData("Proton", "Myvi", "1.5 AV", 2025, 15000, "vehicle")]
    [InlineData("Perodua", "Myvi", "Flagship", 2025, 15000, "vehicle")]
    [InlineData(null, null, null, 2025, 15000, "vehicle")]
    [InlineData("Perodua", "Myvi", "1.5 AV", 2027, 15000, "manufactureYear")]
    [InlineData("Perodua", "Myvi", "1.5 AV", 0, 15000, "manufactureYear")]
    [InlineData("Perodua", "Myvi", "1.5 AV", null, 15000, "manufactureYear")]
    [InlineData("Perodua", "Myvi", "1.5 AV", 2025, -1, "mileageKilometres")]
    [InlineData("Perodua", "Myvi", "1.5 AV", 2025, null, "mileageKilometres")]
    public void InvalidInputsReturnTheirOwningFieldError(string? make, string? model, string? trim,
        int? year, int? mileage, string field)
    {
        var service = new ValuationService(new FixedTimeProvider());
        var result = service.Calculate(new ValuationRequest(make, model, trim, year, mileage));

        Assert.Null(result.Quote);
        Assert.Single(result.Errors);
        Assert.Contains(field, result.Errors.Keys);
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return new DateTimeOffset(2025, 12, 31, 16, 0, 0, TimeSpan.Zero);
        }
    }
}

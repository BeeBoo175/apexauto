using Dealership.Domain.Inspections;
using Dealership.Domain.Vehicles;

namespace Dealership.UnitTests;

public sealed class WarrantyPackageTests
{
    private static readonly bool[] GoldEligibility = [true, true, false];
    private static readonly bool[] PlatinumEligibility = [true, false, false];

    [Fact]
    public void NewPackageTrimsTheNameAndStartsAtVersionOne()
    {
        var package = new WarrantyPackage(Guid.NewGuid(), "  ApexShield Gold  ", 24, 50000, 2400m, InspectionGrade.B, true);

        Assert.Equal(("ApexShield Gold", 24, 50000, 2400m, InspectionGrade.B, 1L),
            (package.Name, package.DurationMonths, package.MileageCapKm, package.Price, package.MinimumGrade,
                package.Version));
    }

    [Fact]
    public void EligibilityRequiresAnActivePackageAndAGradeAtOrAboveTheMinimum()
    {
        var silver = new WarrantyPackage(Guid.NewGuid(), "Silver", 12, 25000, 1200m, InspectionGrade.C, true);
        var gold = new WarrantyPackage(Guid.NewGuid(), "Gold", 24, 50000, 2400m, InspectionGrade.B, true);
        var platinum = new WarrantyPackage(Guid.NewGuid(), "Platinum", 36, 75000, 3600m, InspectionGrade.A, true);
        var retired = new WarrantyPackage(Guid.NewGuid(), "Retired", 12, 25000, 1200m, InspectionGrade.C, false);

        Assert.All(new InspectionGrade?[] { InspectionGrade.A, InspectionGrade.B, InspectionGrade.C },
            grade => Assert.True(silver.IsEligibleFor(grade)));
        Assert.Equal(GoldEligibility, new[] { InspectionGrade.A, InspectionGrade.B, InspectionGrade.C }
            .Select(grade => gold.IsEligibleFor(grade)));
        Assert.Equal(PlatinumEligibility, new[] { InspectionGrade.A, InspectionGrade.B, InspectionGrade.C }
            .Select(grade => platinum.IsEligibleFor(grade)));
        Assert.All(new InspectionGrade?[] { null, InspectionGrade.A }, grade =>
        {
            Assert.False(retired.IsEligibleFor(grade));
            Assert.False(grade is null && silver.IsEligibleFor(grade));
        });
    }

    [Theory]
    [InlineData(0, 25000, 1200)]
    [InlineData(121, 25000, 1200)]
    [InlineData(12, 0, 1200)]
    [InlineData(12, 500001, 1200)]
    [InlineData(12, 25000, -0.01)]
    [InlineData(12, 25000, 20000.01)]
    [InlineData(12, 25000, 1200.001)]
    public void TermsOutsideTheApprovedBoundsAreRejected(int months, int kilometres, double price)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new WarrantyPackage(Guid.NewGuid(), "Silver", months,
            kilometres, (decimal)price, InspectionGrade.C, true));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Silver\n")]
    public void MissingOrControlCharacterNamesAreRejected(string name)
    {
        Assert.Throws<ArgumentException>(() => new WarrantyPackage(Guid.NewGuid(), name, 12, 25000, 1200m, InspectionGrade.C, true));
        Assert.Throws<ArgumentException>(() => new WarrantyPackage(Guid.Empty, "Silver", 12, 25000, 1200m, InspectionGrade.C, true));
        Assert.Throws<ArgumentException>(() => new WarrantyPackage(Guid.NewGuid(), new string('x', 101), 12, 25000, 1200m, InspectionGrade.C, true));
    }

    [Fact]
    public void UpdateReplacesTermsAndAdvancesTheVersionOnlyWhenValid()
    {
        var package = new WarrantyPackage(Guid.NewGuid(), "Silver", 12, 25000, 1200m, InspectionGrade.C, true);

        package.Update("Silver Plus", 18, 30000, 1500m, InspectionGrade.B, false);
        Assert.Throws<ArgumentOutOfRangeException>(() => package.Update("Silver Plus", 0, 30000, 1500m, InspectionGrade.B, true));

        Assert.Equal(("Silver Plus", 18, 30000, 1500m, InspectionGrade.B, false, 2L),
            (package.Name, package.DurationMonths, package.MileageCapKm, package.Price, package.MinimumGrade,
                package.IsActive, package.Version));
    }
}

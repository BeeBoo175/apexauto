using Dealership.Domain.Inspections;

namespace Dealership.UnitTests;

public sealed class InspectionRepairScopeTests
{
    [Fact]
    public void ScopeCopiesAndNormalizesWorkWithoutRegradingTheOriginalFinding()
    {
        var items = new List<InspectionRepairItem> { new("EP-004", "  Refinish front bumper  "), new("ET-001", "Inspect oil leak") };
        var scope = new InspectionRepairScope(items, 250m);
        items.Clear();
        Assert.Equal(["EP-004", "ET-001"], scope.Items.Select(item => item.PointCode));
        Assert.Equal("Refinish front bumper", scope.Items[0].Work);
        Assert.Equal(250m, scope.Budget);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(0.001)]
    public void BudgetRejectsInvalidMinorUnits(double value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new InspectionRepairScope([new("EP-004", "Refinish")], (decimal)value));
    }

    [Fact]
    public void ScopeRejectsMissingDuplicateAndUnboundedWork()
    {
        Assert.Throws<ArgumentException>(() => new InspectionRepairScope([], 0));
        Assert.Throws<ArgumentException>(() => new InspectionRepairScope([new("EP-004", "Refinish"), new("EP-004", "Replace")], 0));
        foreach (var work in new[] { " ", "\u2003", "line\nbreak", new string('a', 1001), string.Concat(Enumerable.Repeat("😀", 501)) })
        {
            Assert.Throws<ArgumentException>(() => new InspectionRepairScope([new("EP-004", work)], 0));
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => new InspectionRepairScope([new("EP-004", "Refinish")], 10000000000000000m));
        Assert.Equal(0m, new InspectionRepairScope([new("EP-004", "Adjust without replacement parts")], 0).Budget);
    }
}

using Dealership.Domain.Inspections;

namespace Dealership.UnitTests;

public class InspectionGradingTests
{
    [Theory]
    [InlineData(0, InspectionGrade.A)]
    [InlineData(499.99, InspectionGrade.A)]
    [InlineData(500, InspectionGrade.B)]
    [InlineData(2499.99, InspectionGrade.B)]
    [InlineData(2500, InspectionGrade.C)]
    [InlineData(5999.99, InspectionGrade.C)]
    [InlineData(6000, InspectionGrade.C)]
    [InlineData(15000, InspectionGrade.C)]
    public void AppliesApprovedCostBoundaries(decimal cost, InspectionGrade expected)
    {
        var assessment = InspectionGrading.Calculate([Finding("EP-001", InspectionSeverity.Advisory, cost)]);

        Assert.Equal(expected, assessment.Grade);
        Assert.False(assessment.IsRejected);
        Assert.Equal(cost, assessment.EstimatedRepairCost);
    }

    [Theory]
    [InlineData(InspectionSeverity.MinorDefect, 0, InspectionGrade.B)]
    [InlineData(InspectionSeverity.MajorDefect, 0, InspectionGrade.C)]
    [InlineData(InspectionSeverity.MinorDefect, 3000, InspectionGrade.C)]
    [InlineData(InspectionSeverity.MajorDefect, 500, InspectionGrade.C)]
    public void UsesWorseOfSeverityAndCombinedCost(InspectionSeverity severity, decimal cost, InspectionGrade expected)
    {
        var assessment = InspectionGrading.Calculate([
            Finding("EP-001", severity, cost),
            Finding("EP-002", InspectionSeverity.Advisory, 0)
        ]);

        Assert.Equal(expected, assessment.Grade);
    }

    [Fact]
    public void AggregatesAllFindingsBeforeGrading()
    {
        var assessment = InspectionGrading.Calculate([
            Finding("EP-001", InspectionSeverity.Advisory, 2499.99m),
            Finding("EP-002", InspectionSeverity.Advisory, 0.01m)
        ]);

        Assert.Equal(2500m, assessment.EstimatedRepairCost);
        Assert.Equal(InspectionGrade.C, assessment.Grade);
    }

    [Theory]
    [InlineData(InspectionSeverity.Advisory, 15000.01)]
    [InlineData(InspectionSeverity.MinorDefect, 15001)]
    [InlineData(InspectionSeverity.MajorDefect, 15001)]
    [InlineData(InspectionSeverity.CriticalDisqualifier, 0)]
    [InlineData(InspectionSeverity.CriticalDisqualifier, 15000)]
    public void RejectsWithoutAssigningAGrade(InspectionSeverity severity, decimal cost)
    {
        var assessment = InspectionGrading.Calculate([Finding("FC-038", severity, cost)]);

        Assert.True(assessment.IsRejected);
        Assert.Null(assessment.Grade);
    }

    [Fact]
    public void NoFindingsQualifyForAWithoutCertifyingCompletion()
    {
        var assessment = InspectionGrading.Calculate([]);

        Assert.Equal(InspectionGrade.A, assessment.Grade);
        Assert.Equal(0m, assessment.EstimatedRepairCost);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(0.001)]
    public void RefusesInvalidMoneyWithoutRounding(decimal cost)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Finding("EP-001", InspectionSeverity.Advisory, cost));
    }

    [Fact]
    public void RefusesUnknownPointsAndSeverities()
    {
        Assert.Throws<ArgumentException>(() => Finding("EP-046", InspectionSeverity.Advisory, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Finding("EP-001", (InspectionSeverity)99, 0));
    }

    [Fact]
    public void RefusesDuplicatePointsEvenWhenAlreadyDisqualified()
    {
        Assert.Throws<ArgumentException>(() => InspectionGrading.Calculate([
            Finding("FC-038", InspectionSeverity.CriticalDisqualifier, 0),
            Finding("FC-038", InspectionSeverity.Advisory, 1)
        ]));
    }

    [Fact]
    public void RefusesMissingInputsAndUnrepresentableTotals()
    {
        Assert.Throws<ArgumentNullException>(() => InspectionGrading.Calculate(null!));
        Assert.Throws<ArgumentNullException>(() => InspectionGrading.Calculate([null!]));
        Assert.Throws<ArgumentOutOfRangeException>(() => InspectionGrading.Calculate([
            Finding("EP-001", InspectionSeverity.Advisory, decimal.MaxValue),
            Finding("EP-002", InspectionSeverity.Advisory, 1)
        ]));
    }

    [Fact]
    public void RefusesSilentMinorUnitLossBelowDecimalOverflow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => InspectionGrading.Calculate([
            Finding("EP-001", InspectionSeverity.Advisory, decimal.MaxValue - 1),
            Finding("EP-002", InspectionSeverity.Advisory, 0.01m)
        ]));
    }

    private static InspectionDraftEntry Finding(string pointCode, InspectionSeverity severity, decimal cost)
    {
        return new(pointCode, InspectionEntryKind.Finding, severity, cost, "Observed condition.");
    }
}

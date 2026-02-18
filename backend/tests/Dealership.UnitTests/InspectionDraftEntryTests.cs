using Dealership.Domain.Inspections;
using Dealership.Application.Inspections;
using System.Text.Json;

namespace Dealership.UnitTests;

public sealed class InspectionDraftEntryTests
{
    [Fact]
    public void ApplicabilityRetainsReasonWithoutChangingTheCanonicalGrade()
    {
        var absent = new InspectionDraftEntry("IE-040", InspectionEntryKind.NotFitted, null, 0, "  Equipment absent  ");
        var defect = new InspectionDraftEntry("FC-001", InspectionEntryKind.Finding, InspectionSeverity.MajorDefect, 100, "Observed damage");
        Assert.Equal("Equipment absent", absent.Notes);
        var assessment = InspectionGrading.Calculate([absent, defect]);
        Assert.Equal(100, assessment.EstimatedRepairCost);
        Assert.Equal(InspectionGrade.C, assessment.Grade);
        Assert.Throws<ArgumentException>(() => InspectionGrading.Calculate([absent, absent]));
        Assert.Throws<ArgumentNullException>(() => InspectionGrading.Calculate([null!]));
    }

    [Theory]
    [InlineData(InspectionEntryKind.NotFitted, InspectionSeverity.Advisory, 0)]
    [InlineData(InspectionEntryKind.NotFitted, null, 1)]
    [InlineData(InspectionEntryKind.Finding, null, 0)]
    [InlineData((InspectionEntryKind)100, null, 0)]
    [InlineData(InspectionEntryKind.Finding, (InspectionSeverity)100, 0)]
    public void RejectsContradictoryOrUnknownDispositions(InspectionEntryKind kind, InspectionSeverity? severity, int cost)
    {
        Assert.ThrowsAny<ArgumentException>(() => new InspectionDraftEntry("FC-001", kind, severity, cost, "Observed"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("line\nbreak")]
    public void RequiresAReadableReason(string notes)
    {
        Assert.Throws<ArgumentException>(() => new InspectionDraftEntry("IE-040", InspectionEntryKind.NotFitted, null, 0, notes));
    }

    [Fact]
    public void EnforcesCatalogAndNoteLimits()
    {
        Assert.Throws<ArgumentException>(() => new InspectionDraftEntry("unknown", InspectionEntryKind.NotFitted, null, 0, "Absent"));
        Assert.Throws<ArgumentException>(() => new InspectionDraftEntry("IE-040", InspectionEntryKind.NotFitted, null, 0, new string('x', 1001)));
        Assert.Throws<ArgumentException>(() => new InspectionDraftEntry("FC-001", InspectionEntryKind.NotFitted, null, 0, "Cannot omit chassis"));
    }

    [Theory]
    [InlineData("0.010000000000000000000000000001")]
    [InlineData("1e-100")]
    [InlineData("7922816251426433759354395033.51")]
    [InlineData("-1")]
    [InlineData("0.001")]
    public void RejectsRawJsonPrecisionLoss(string amount)
    {
        var json = $$"""{"pointCode":"EP-004","kind":0,"severity":0,"estimatedRepairCost":{{amount}},"notes":"Observed"}""";
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<InspectionDraftEntryInput>(json, JsonSerializerOptions.Web));
    }

    [Theory]
    [InlineData("1.00", "1")]
    [InlineData("1e2", "100")]
    [InlineData("100e-4", "0.01")]
    [InlineData("0e-100", "0")]
    [InlineData("79228162514264337593543950335", "79228162514264337593543950335")]
    public void AcceptsExactJsonMoney(string amount, string expected)
    {
        var json = $$"""{"pointCode":"EP-004","kind":0,"severity":0,"estimatedRepairCost":{{amount}},"notes":"Observed"}""";
        var entry = JsonSerializer.Deserialize<InspectionDraftEntryInput>(json, JsonSerializerOptions.Web)!;
        Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), entry.EstimatedRepairCost);
    }
}

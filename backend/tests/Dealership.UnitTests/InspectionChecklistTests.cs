using Dealership.Domain.Inspections;

namespace Dealership.UnitTests;

public sealed class InspectionChecklistTests
{
    private static readonly string[] AllPoints = [.. InspectionCatalog.Points.Select(point => point.Code)];

    [Fact]
    public void RequiresExplicitExaminationEvenWhenNoDefectsWereRecorded()
    {
        Assert.Throws<ArgumentException>(() => InspectionChecklist.Attest([], []));
        Assert.Throws<ArgumentException>(() => InspectionChecklist.Attest([], AllPoints[..^1]));
        Assert.Equal(AllPoints.Order(StringComparer.Ordinal), InspectionChecklist.Attest([], AllPoints));
    }

    [Fact]
    public void ReasonedNotFittedReplacesExaminationAndFindingsStillRequireExamination()
    {
        InspectionDraftEntry[] entries =
        [
            new("IE-040", InspectionEntryKind.NotFitted, null, 0, "Physical examination confirms no powered roof."),
            new("EP-004", InspectionEntryKind.Finding, InspectionSeverity.MinorDefect, 250, "Bumper scratch")
        ];
        var fitted = AllPoints.Where(code => code != "IE-040").ToArray();
        Assert.Equal(fitted.Order(StringComparer.Ordinal), InspectionChecklist.Attest(entries, fitted.Reverse().ToArray()));
        Assert.Throws<ArgumentException>(() => InspectionChecklist.Attest(entries, AllPoints));
        Assert.Throws<ArgumentException>(() => InspectionChecklist.Attest(entries, fitted.Where(code => code != "EP-004").ToArray()));
    }

    [Theory]
    [InlineData("FC-000")]
    [InlineData("fc-001")]
    [InlineData(" FC-001")]
    [InlineData(null)]
    public void RejectsUnknownAndMalformedCodes(string? code)
    {
        Assert.Throws<ArgumentException>(() => InspectionChecklist.Attest([], [.. AllPoints.Skip(1), code!]));
    }

    [Fact]
    public void RejectsDuplicatesAndOversizedAttestationsWithoutMutatingCallerCollections()
    {
        Assert.Throws<ArgumentException>(() => InspectionChecklist.Attest([], [.. AllPoints.Skip(1), AllPoints[1]]));
        Assert.Throws<ArgumentException>(() => InspectionChecklist.Attest([], [.. AllPoints, AllPoints[0]]));
        var input = AllPoints.ToArray();
        var attestation = InspectionChecklist.Attest([], input);
        input[0] = "changed";
        Assert.Contains("FC-001", attestation);
    }
}

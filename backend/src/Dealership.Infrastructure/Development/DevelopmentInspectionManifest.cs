using Dealership.Application.Inspections;
using Dealership.Domain.Inspections;

namespace Dealership.Infrastructure.Development;

public static class DevelopmentInspectionManifest
{
    public static IReadOnlyList<InspectionDraftEntryInput> Entries { get; } = Array.AsReadOnly<InspectionDraftEntryInput>([
        new("EP-004", InspectionEntryKind.Finding, InspectionSeverity.MinorDefect, 250m,
            "Front bumper has a shallow surface scratch; refinish estimate retained for assessment."),
        new("IE-040", InspectionEntryKind.NotFitted, null, 0m,
            "Vehicle specification and physical examination confirm no powered roof is fitted.")
    ]);
}

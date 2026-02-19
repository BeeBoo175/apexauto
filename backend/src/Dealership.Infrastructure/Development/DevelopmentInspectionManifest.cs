using Dealership.Application.Inspections;
using Dealership.Domain.Inspections;
using System.Security.Cryptography;
using System.Text;

namespace Dealership.Infrastructure.Development;

public static class DevelopmentInspectionManifest
{
    public const string ReopenReason = "Manager requests a fresh examination after reviewing the retained bumper evidence.";

    public static Guid ReopenId(Guid inspectionId) => StableId(inspectionId, "reopen");

    public static InspectionRepairApprovalRequest RepairApproval(Guid inspectionId) => new(StableId(inspectionId, "approval"),
        RepairRequest(inspectionId).OperationId, 1, 1, 1);

    public static InspectionRepairRequest RepairRequest(Guid inspectionId) => new(StableId(inspectionId, "repair"),
        1, 1, 0, [new("EP-004", "Prepare and refinish the scratched front bumper surface")], 250m);

    public static Guid PhotoId(Guid inspectionId)
    {
        return StableId(inspectionId, "bumper");
    }

    private static Guid StableId(Guid inspectionId, string purpose)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"apexauto/development/inspection/{inspectionId:N}/{purpose}"));
        bytes[6] = (byte)((bytes[6] & 0x0f) | 0x80);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
        return Guid.ParseExact(Convert.ToHexString(bytes.AsSpan(0, 16)), "N");
    }

    public static IReadOnlyList<InspectionDraftEntryInput> Entries { get; } = Array.AsReadOnly<InspectionDraftEntryInput>([
        new("EP-004", InspectionEntryKind.Finding, InspectionSeverity.MinorDefect, 250m,
            "Front bumper has a shallow surface scratch; refinish estimate retained for assessment."),
        new("IE-040", InspectionEntryKind.NotFitted, null, 0m,
            "Vehicle specification and physical examination confirm no powered roof is fitted.")
    ]);

    public static IReadOnlyList<string> ExaminedPointCodes { get; } = Array.AsReadOnly(InspectionCatalog.Points
        .Where(point => !Entries.Any(entry => entry.PointCode == point.Code && entry.Kind == InspectionEntryKind.NotFitted))
        .Select(point => point.Code).Order(StringComparer.Ordinal).ToArray());
}

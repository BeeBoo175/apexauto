using Dealership.Application.Inspections;
using Dealership.Application.Intake;
using Dealership.Domain.Inspections;

namespace Dealership.Infrastructure.Development;

public static class DevelopmentInspectionManifest
{
    public static IReadOnlyList<DevelopmentIntakeFixture> Baselines { get; } = Array.AsReadOnly(
        DevelopmentIntakeManifest.Appointments.Where(item => item.Status == StaffWorklistStatus.CheckedIn)
            .GroupBy(item => item.BranchId).Select(group => group.First()).ToArray());

    public static bool IsClaimable(DevelopmentIntakeFixture fixture) => fixture.Id ==
        DevelopmentIntakeManifest.Appointments.Where(item => item.BranchId == fixture.BranchId)
            .Take(DevelopmentIntakeManifest.BaselineTotalPerBranch).Last().Id;

    public const string ReopenReason = "Manager requests a fresh examination after reviewing the retained bumper evidence.";

    public static Guid ReopenId(Guid inspectionId) => DevelopmentInspectionScenarios.OperationId(inspectionId, "reopen");

    public static InspectionRepairCompletionRequest RepairCompletion(Guid inspectionId) => new(DevelopmentInspectionScenarios.OperationId(inspectionId, "completion"),
        RepairRequest(inspectionId).OperationId, 1, 1, 1, "Bumper refinished and reinspected; finish and fit are satisfactory.");

    public static InspectionRepairApprovalRequest RepairApproval(Guid inspectionId) => new(DevelopmentInspectionScenarios.OperationId(inspectionId, "approval"),
        RepairRequest(inspectionId).OperationId, 1, 1, 1);

    public static InspectionRepairRequest RepairRequest(Guid inspectionId, long assignmentVersion = 1) =>
        new(DevelopmentInspectionScenarios.OperationId(inspectionId, "repair"), assignmentVersion, 1, 0, [new("EP-004", "Prepare and refinish the scratched front bumper surface")], 250m);

    public static Guid PhotoId(Guid inspectionId)
    {
        return DevelopmentInspectionScenarios.OperationId(inspectionId, "bumper");
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

using Dealership.Application.Inspections;
using Dealership.Domain.Inspections;
using Dealership.Domain.Intake;
using System.Security.Cryptography;
using System.Text;

namespace Dealership.Infrastructure.Development;

public enum DevelopmentInspectionStage
{
    Draft,
    Sealed,
    RepairRequested,
    RepairApproved,
    RepairCompleted,
    RepairSuperseded,
    RepairScopeChanged,
    RepairApprovalRetained,
    Reinspected
}

public sealed record DevelopmentInspectionPhoto(string PointCode, PhotoAngle Angle);

public sealed record DevelopmentInspectionScenario(DevelopmentIntakeFixture Intake, string Name,
    IReadOnlyList<InspectionDraftEntryInput> Entries, DevelopmentInspectionStage Stage, decimal Budget = 0m)
{
    public Guid Id => Intake.Id;

    public bool PublishListing { get; init; }

    public IReadOnlyList<DevelopmentInspectionPhoto> Photos { get; init; } = [];

    public Guid OperationId(string purpose) => DevelopmentInspectionScenarios.OperationId(Id, purpose);

    public Guid PhotoId(DevelopmentInspectionPhoto photo) => OperationId($"photo-{photo.PointCode}-{photo.Angle}");

    public bool ReplacesRepair => Stage is DevelopmentInspectionStage.RepairSuperseded or
        DevelopmentInspectionStage.RepairScopeChanged or DevelopmentInspectionStage.RepairApprovalRetained;

    public bool ApprovesRepair => ReplacesRepair || Stage == DevelopmentInspectionStage.RepairApproved ||
        Stage == DevelopmentInspectionStage.RepairCompleted && Budget > 0;

    public bool CompletesRepair => Stage is DevelopmentInspectionStage.RepairCompleted or DevelopmentInspectionStage.RepairScopeChanged;

    public InspectionDraftSaveRequest Save(bool reinspection = false)
    {
        var entries = reinspection ? [.. Entries.Where(entry => entry.Kind == InspectionEntryKind.NotFitted)] : Entries;
        return new(OperationId(reinspection ? "save-2" : "save-1"), 1, reinspection ? 1 : 0, entries)
        {
            PhotoIds = reinspection ? [] : [.. Photos.Select(PhotoId)],
            ExaminedPointCodes = Stage == DevelopmentInspectionStage.Draft ? null : InspectionCatalog.Points
                .Where(point => !entries.Any(entry => entry.PointCode == point.Code && entry.Kind == InspectionEntryKind.NotFitted))
                .Select(point => point.Code).Order(StringComparer.Ordinal).ToArray()
        };
    }

    public InspectionSignRequest Sign(bool reinspection = false) =>
        new(OperationId(reinspection ? "sign-2" : "sign-1"), 1, reinspection ? 2 : 1, reinspection ? 2 : 1);

    public InspectionRepairRequest Repair(bool replacement = false)
    {
        var findings = Entries.Where(entry => entry.Kind == InspectionEntryKind.Finding);
        var selected = replacement && Stage == DevelopmentInspectionStage.RepairScopeChanged ? findings : findings.Take(1);
        var items = selected.Select(entry => new InspectionRepairItem(entry.PointCode,
            "Rectify the recorded defect and verify the affected component before release")).ToArray();
        var budget = replacement ? ReplacementBudget() : Budget;
        return new(OperationId(replacement ? "repair-2" : "repair-1"), 1, 1, replacement ? 1 : 0, items, budget);
    }

    private decimal ReplacementBudget() => Stage switch
    {
        DevelopmentInspectionStage.RepairScopeChanged => Budget,
        DevelopmentInspectionStage.RepairApprovalRetained => Budget - 50m,
        _ => Budget + 150m
    };

    public InspectionRepairApprovalRequest Approval() => new(OperationId("approval"), Repair().OperationId, 1, 1, 1);

    public InspectionRepairCompletionRequest Completion() => new(OperationId("completion"), Repair().OperationId,
        1, 1, 1, "Recorded work completed and the affected component reinspected; operation is satisfactory.");

    public InspectionReopenRequest Reopening() => new(OperationId("reopen"), 1, 1,
        "Manager authorizes a fresh examination after rectification; retain the original signed findings.");
}

public static class DevelopmentInspectionScenarios
{
    public static IReadOnlyList<DevelopmentInspectionScenario> All { get; } = Array.AsReadOnly(
        DevelopmentIntakeManifest.InspectionExamples.GroupBy(item => item.BranchId)
            .SelectMany(branch => branch.Select(Create))
            .Concat(DevelopmentIntakeManifest.CatalogueExamples.Select(intake =>
                Create(intake, 1) with { Name = "Catalogue examination" }))
            .Concat(DevelopmentIntakeManifest.WorkshopExamples.Select(intake =>
                new DevelopmentInspectionScenario(intake, "Examination in progress", [], DevelopmentInspectionStage.Draft)))
            .ToArray());

    public static Guid OperationId(Guid inspectionId, string purpose)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"apexauto/development/inspection/{inspectionId:N}/{purpose}"));
        bytes[6] = (byte)((bytes[6] & 0x0f) | 0x80);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
        return Guid.ParseExact(Convert.ToHexString(bytes.AsSpan(0, 16)), "N");
    }

    private static DevelopmentInspectionScenario Create(DevelopmentIntakeFixture intake, int index)
    {
        var scenario = CreateExample(intake, index);
        return scenario with
        {
            PublishListing = index is 1 or 2 or 5 or 11,
            Entries = [.. scenario.Entries, new("IE-040", InspectionEntryKind.NotFitted, null, 0m,
                "Vehicle specification and physical examination confirm no powered roof is fitted.")]
        };
    }

    private static DevelopmentInspectionScenario CreateExample(DevelopmentIntakeFixture intake, int index) => index switch
    {
        0 => new(intake, "Examination in progress", Finding("EP-004", InspectionSeverity.MinorDefect, 250m,
            "Front bumper surface requires examination and a refinish estimate."), DevelopmentInspectionStage.Draft),
        1 => new(intake, "Clean Grade A", [], DevelopmentInspectionStage.Sealed),
        2 => new(intake, "Advisory without repairs", Finding("EP-004", InspectionSeverity.Advisory, 0m,
            "Light cosmetic patina; no corrective work required."), DevelopmentInspectionStage.Sealed),
        3 => new(intake, "Approved paid repair", Finding("EP-004", InspectionSeverity.MinorDefect, 750m,
            "Front bumper finish requires preparation and repainting."), DevelopmentInspectionStage.RepairApproved, 750m),
        4 => new(intake, "Zero-cost repair awaiting completion", Finding("EP-004", InspectionSeverity.MinorDefect, 0m,
            "Loose bumper trim requires refitting using existing workshop materials."), DevelopmentInspectionStage.RepairRequested),
        5 => new(intake, "Zero-cost repair completed", Finding("EP-004", InspectionSeverity.MinorDefect, 0m,
            "Bumper trim requires refitting before release."), DevelopmentInspectionStage.RepairCompleted),
        6 => new(intake, "Major defect Grade C", Finding("ET-043", InspectionSeverity.MajorDefect, 6000m,
            "Harsh gear changes require transmission diagnosis and rectification."), DevelopmentInspectionStage.RepairRequested, 6000m),
        7 => new(intake, "Inclusive repair-cost limit", Finding("ET-044", InspectionSeverity.MajorDefect, 15000m,
            "Transmission slip requires overhaul at the retained repair-cost ceiling."), DevelopmentInspectionStage.RepairRequested, 15000m),
        8 => new(intake, "Structural rejection", Finding("FC-038", InspectionSeverity.CriticalDisqualifier, 0m,
            "Structural cut-and-weld joining confirmed during chassis examination."), DevelopmentInspectionStage.Sealed),
        9 => new(intake, "Repair-cost rejection", Finding("ET-044", InspectionSeverity.MajorDefect, 15000.01m,
            "Transmission overhaul estimate exceeds the accepted acquisition limit."), DevelopmentInspectionStage.Sealed),
        10 => new(intake, "Increased repair budget", Finding("EP-004", InspectionSeverity.MinorDefect, 750m,
            "Bumper preparation requires a revised budget after the first approved work estimate."), DevelopmentInspectionStage.RepairSuperseded, 750m),
        11 => new(intake, "Signed reinspection", Finding("EP-004", InspectionSeverity.MinorDefect, 750m,
            "Bumper damage recorded before the manager-authorized reinspection."), DevelopmentInspectionStage.Reinspected),
        12 => new(intake, "Changed repair scope after partial completion",
            [.. Finding("EP-004", InspectionSeverity.MinorDefect, 750m, "Bumper finish requires repainting."),
                new("EP-003", InspectionEntryKind.Finding, InspectionSeverity.MinorDefect, 0m,
                    "Bonnet catch requires adjustment using existing workshop materials.")],
            DevelopmentInspectionStage.RepairScopeChanged, 750m)
        {
            Photos = [new("EP-004", PhotoAngle.Front), new("EP-004", PhotoAngle.Driver)]
        },
        13 => new(intake, "Retained approval with lower budget", Finding("EP-004", InspectionSeverity.MinorDefect, 750m,
            "Bumper refinish work is unchanged; the revised supplier estimate reduces the requested budget."),
            DevelopmentInspectionStage.RepairApprovalRetained, 750m),
        _ => throw new InvalidOperationException("The configured branch inspection example has no scenario.")
    };

    private static IReadOnlyList<InspectionDraftEntryInput> Finding(string point, InspectionSeverity severity,
        decimal cost, string notes) => [new(point, InspectionEntryKind.Finding, severity, cost, notes)];
}

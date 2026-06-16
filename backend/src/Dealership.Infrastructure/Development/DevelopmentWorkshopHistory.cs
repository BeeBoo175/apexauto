using Dealership.Application.Inspections;
using Dealership.Application.Intake;
using Dealership.Domain.Inspections;
using Dealership.Infrastructure.Inspections;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Development;

// Builds signed inspections at their historical times inside the snapshot transaction, mirroring the canonical
// assignment, save and sign-off services, which can only stamp the database clock.
public sealed class DevelopmentWorkshopHistory(DealershipDbContext database)
{
    private static readonly TimeSpan AssignmentDelay = TimeSpan.FromMinutes(20);
    private static readonly TimeSpan SaveDelay = TimeSpan.FromMinutes(50);
    private static readonly TimeSpan SignDelay = TimeSpan.FromMinutes(80);

    public static bool Covers(DevelopmentIntakeFixture fixture)
    {
        return fixture.Status == StaffWorklistStatus.CheckedIn &&
            !DevelopmentInspectionManifest.Baselines.Any(item => item.Id == fixture.Id) &&
            !DevelopmentInspectionManifest.IsClaimable(fixture) &&
            DevelopmentInspectionScenarios.All.All(item => item.Id != fixture.Id);
    }

    // Sealed examinations without photographs are the only scenarios whose evidence the snapshot can build.
    public static bool Covers(DevelopmentInspectionScenario scenario)
    {
        return scenario.Stage == DevelopmentInspectionStage.Sealed && scenario.Photos.Count == 0 &&
            !DevelopmentInspectionManifest.IsClaimable(scenario.Intake);
    }

    public static DateTimeOffset SignedAt(DateTimeOffset checkedInAt) => checkedInAt + SignDelay;

    public Task<DateTimeOffset> AddSignedInspectionAsync(DevelopmentIntakeFixture fixture, DateTimeOffset checkedInAt,
        CancellationToken cancellationToken)
    {
        var save = new InspectionDraftSaveRequest(fixture.Id, 1, 0, DevelopmentInspectionManifest.Entries)
        {
            PhotoIds = [],
            ExaminedPointCodes = DevelopmentInspectionManifest.ExaminedPointCodes
        };
        var technicianId = DevelopmentIntakeManifest.WorkshopExamples.Any(item => item.Id == fixture.Id)
            ? DevelopmentFixtureManifest.ReliefTechnicians.Single(item => item.BranchId == fixture.BranchId).Id
            : DevelopmentFixtureManifest.PrimaryTechnician(fixture.BranchId).Id;
        return AddSignedInspectionAsync(fixture, technicianId, save, new InspectionSignRequest(fixture.Id, 1, 1, 1),
            checkedInAt, cancellationToken);
    }

    public Task<DateTimeOffset> AddSignedInspectionAsync(DevelopmentInspectionScenario scenario, DateTimeOffset checkedInAt,
        CancellationToken cancellationToken)
    {
        var technicianId = DevelopmentIntakeManifest.WorkshopExamples.Any(item => item.Id == scenario.Id)
            ? DevelopmentFixtureManifest.ReliefTechnicians.Single(item => item.BranchId == scenario.Intake.BranchId).Id
            : DevelopmentFixtureManifest.PrimaryTechnician(scenario.Intake.BranchId).Id;
        return AddSignedInspectionAsync(scenario.Intake, technicianId, scenario.Save(), scenario.Sign(), checkedInAt,
            cancellationToken);
    }

    private async Task<DateTimeOffset> AddSignedInspectionAsync(DevelopmentIntakeFixture fixture, Guid technicianId,
        InspectionDraftSaveRequest request, InspectionSignRequest sign, DateTimeOffset checkedInAt,
        CancellationToken cancellationToken)
    {
        var technician = await database.Users.AsNoTracking().SingleAsync(item => item.Id == technicianId, cancellationToken);
        var vehicle = await database.Vehicles.AsNoTracking()
            .SingleAsync(item => item.IntakeAppointmentId == fixture.Id, cancellationToken);
        var assignedAt = checkedInAt + AssignmentDelay;
        var draft = new InspectionDraft(fixture.Id, vehicle, technicianId, assignedAt);
        database.InspectionDrafts.Add(draft);
        database.InspectionAssignments.Add(new InspectionAssignment(fixture.Id, draft, technicianId, null, 0, null, assignedAt));
        await database.SaveChangesAsync(cancellationToken);

        var receipt = CreateReceipt(fixture.Id, technicianId, request, checkedInAt + SaveDelay);
        database.InspectionDraftSaves.Add(new InspectionDraftSave(draft.BranchId, receipt));
        await database.SaveChangesAsync(cancellationToken);

        var signedAt = SignedAt(checkedInAt);
        var report = InspectionReport.Sign(sign.OperationId, draft.Revision, draft, vehicle, technician.UserName!, receipt, [],
            signedAt);
        database.InspectionSignedRevisions.Add(new InspectionSignedRevision(report));
        await database.SaveChangesAsync(cancellationToken);
        return signedAt;
    }

    private static InspectionDraftSaveReceipt CreateReceipt(Guid inspectionId, Guid technicianId,
        InspectionDraftSaveRequest request, DateTimeOffset recordedAt)
    {
        InspectionDraftEntry[] entries = [.. request.Entries.Select(entry => new InspectionDraftEntry(
            entry.PointCode, entry.Kind, entry.Severity, entry.EstimatedRepairCost, entry.Notes))
            .OrderBy(entry => entry.PointCode, StringComparer.Ordinal)];
        var assessment = InspectionGrading.Calculate(entries);
        return new InspectionDraftSaveReceipt(request.OperationId, inspectionId, technicianId, request.AssignmentVersion,
            request.ExpectedVersion, request.ExpectedVersion + 1, InspectionCatalog.Version, entries,
            new(assessment.EstimatedRepairCost, assessment.Grade, assessment.IsRejected), recordedAt)
        {
            PhotoIds = [],
            ExaminedPointCodes = InspectionChecklist.Attest(entries, request.ExaminedPointCodes!)
        };
    }
}

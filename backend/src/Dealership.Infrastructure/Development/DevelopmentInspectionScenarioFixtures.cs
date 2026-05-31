using Dealership.Application.Accounts;
using Dealership.Application.Inspections;
using Dealership.Application.Photos;
using Dealership.Domain.Inspections;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Development;

public sealed class DevelopmentInspectionScenarioFixtures(DealershipDbContext database,
    InspectionAssignmentService assignments, InspectionDraftSaveService saves, InspectionSignoffService signoffs,
    InspectionReopeningService reopenings, InspectionRepairRequestService repairs,
    InspectionRepairApprovalService approvals, InspectionRepairCompletionService completions,
    InspectionPhotoService photos, IPhotoImageValidator photoValidator)
{
    public async Task<DevelopmentFixtureReport> RunAsync(bool seed, DevelopmentInspectionScenario scenario,
        Guid vehicleId, Guid technicianId, CancellationToken cancellationToken)
    {
        if (!await PreservesProgressAsync(scenario, technicianId, cancellationToken))
        {
            return Report(scenario, "drift; preserved", true);
        }

        if (!await AssignAsync(seed, scenario, vehicleId, technicianId, cancellationToken) ||
            !await PhotosAsync(seed, scenario, technicianId, cancellationToken) ||
            !await SaveAsync(seed, scenario.Id, technicianId, scenario.Save(), cancellationToken))
        {
            return Report(scenario, "assignment, photos or draft missing/changed; preserved", true);
        }

        if (scenario.Stage == DevelopmentInspectionStage.Draft)
        {
            return Report(scenario, "incomplete examination", false);
        }

        if (!await SignAsync(seed, scenario.Id, technicianId, scenario.Sign(), cancellationToken))
        {
            return Report(scenario, "sign-off missing/changed; preserved", true);
        }

        var managerId = DevelopmentFixtureManifest.Staff.Single(item => item.BranchId == scenario.Intake.BranchId &&
            item.Role == AccountRoles.BranchManager).Id;
        if (scenario.Stage == DevelopmentInspectionStage.Reinspected)
        {
            var ready = await ReopenAsync(seed, scenario, managerId, cancellationToken) &&
                await SaveAsync(seed, scenario.Id, technicianId, scenario.Save(true), cancellationToken) &&
                await SignAsync(seed, scenario.Id, technicianId, scenario.Sign(true), cancellationToken);
            return Report(scenario, ready ? "signed revision 2; original retained" : "reinspection missing/changed; preserved", !ready);
        }

        if (scenario.Stage == DevelopmentInspectionStage.Sealed)
        {
            var reopened = await DevelopmentTestDriveReopeningFixtures.MatchesReopeningAsync(database, scenario.Id, cancellationToken);
            return Report(scenario, reopened ? "reopened after test-drive booking; original signed report retained" : "signed", false);
        }

        if (!await RepairAsync(seed, scenario.Id, technicianId, scenario.Repair(), cancellationToken))
        {
            return Report(scenario, "repair request missing/changed; preserved", true);
        }

        if (scenario.ApprovesRepair)
        {
            if (!await ApproveAsync(seed, scenario, managerId, cancellationToken))
            {
                return Report(scenario, "approval missing/changed; preserved", true);
            }
        }

        if (scenario.CompletesRepair &&
            !await CompleteAsync(seed, scenario, technicianId, cancellationToken))
        {
            return Report(scenario, "completion missing/changed; preserved", true);
        }

        if (scenario.ReplacesRepair &&
            !await RepairAsync(seed, scenario.Id, technicianId, scenario.Repair(true), cancellationToken))
        {
            return Report(scenario, "replacement request missing/changed; preserved", true);
        }

        return Report(scenario, "matches", false);
    }

    private async Task<bool> PreservesProgressAsync(DevelopmentInspectionScenario scenario, Guid technicianId,
        CancellationToken cancellationToken)
    {
        var draft = await database.InspectionDrafts.AsNoTracking().SingleOrDefaultAsync(item => item.Id == scenario.Id, cancellationToken);
        if (draft is not null && (draft.TechnicianId != technicianId || draft.AssignmentVersion != 1))
        {
            return false;
        }

        var photoIds = scenario.Photos.Select(scenario.PhotoId).ToArray();
        var ids = new List<Guid> { scenario.Id, scenario.Save().OperationId };
        if (await DevelopmentTestDriveReopeningFixtures.MatchesReopeningAsync(database, scenario.Id, cancellationToken))
        {
            ids.Add(DevelopmentTestDriveReopeningFixtures.ReopeningId);
        }

        if (scenario.Stage != DevelopmentInspectionStage.Draft)
        {
            ids.Add(scenario.Sign().OperationId);
        }
        if (scenario.Stage == DevelopmentInspectionStage.Reinspected)
        {
            ids.AddRange([scenario.Reopening().OperationId, scenario.Save(true).OperationId, scenario.Sign(true).OperationId]);
        }
        if (scenario.Stage is DevelopmentInspectionStage.RepairRequested or DevelopmentInspectionStage.RepairApproved or
            DevelopmentInspectionStage.RepairCompleted || scenario.ReplacesRepair)
        {
            ids.Add(scenario.Repair().OperationId);
        }
        if (scenario.ApprovesRepair)
        {
            ids.Add(scenario.Approval().OperationId);
        }
        if (scenario.CompletesRepair)
        {
            ids.Add(scenario.Completion().OperationId);
        }
        if (scenario.ReplacesRepair)
        {
            ids.Add(scenario.Repair(true).OperationId);
        }

        return !await database.InspectionDraftSaves.AnyAsync(item => item.InspectionId == scenario.Id && !ids.Contains(item.Id), cancellationToken) &&
            !await database.InspectionSignedRevisions.AnyAsync(item => item.InspectionId == scenario.Id && !ids.Contains(item.Id), cancellationToken) &&
            !await database.InspectionReopenings.AnyAsync(item => item.InspectionId == scenario.Id && !ids.Contains(item.Id), cancellationToken) &&
            !await database.InspectionRepairRequests.AnyAsync(item => item.InspectionId == scenario.Id && !ids.Contains(item.Id), cancellationToken) &&
            !await database.InspectionRepairApprovals.AnyAsync(item => item.InspectionId == scenario.Id && !ids.Contains(item.Id), cancellationToken) &&
            !await database.InspectionRepairCompletions.AnyAsync(item => item.InspectionId == scenario.Id && !ids.Contains(item.Id), cancellationToken) &&
            !await database.InspectionPhotos.AnyAsync(item => item.InspectionId == scenario.Id && !photoIds.Contains(item.Id), cancellationToken);
    }

    private async Task<bool> AssignAsync(bool seed, DevelopmentInspectionScenario scenario, Guid vehicleId,
        Guid technicianId, CancellationToken cancellationToken)
    {
        var existing = await database.InspectionDrafts.AsNoTracking().SingleOrDefaultAsync(item => item.VehicleId == vehicleId, cancellationToken);
        if (existing is not null)
        {
            return existing.Id == scenario.Id && existing.TechnicianId == technicianId && existing.AssignmentVersion == 1;
        }

        return seed && (await assignments.AssignAsync(technicianId, vehicleId,
            new(scenario.Id, technicianId, 0, null), cancellationToken)).Status == InspectionAssignmentStatus.Succeeded;
    }

    private async Task<bool> PhotosAsync(bool seed, DevelopmentInspectionScenario scenario, Guid technicianId,
        CancellationToken cancellationToken)
    {
        foreach (var photo in scenario.Photos)
        {
            if (!await PhotoAsync(seed, scenario, photo, technicianId, cancellationToken))
            {
                return false;
            }
        }

        return true;
    }

    private async Task<bool> PhotoAsync(bool seed, DevelopmentInspectionScenario scenario, DevelopmentInspectionPhoto photo,
        Guid technicianId, CancellationToken cancellationToken)
    {
        var id = scenario.PhotoId(photo);
        using var content = DevelopmentFixturePhotos.Open(photo.Angle);
        var validated = await photoValidator.ValidateAsync(content, "image/jpeg", cancellationToken)
            ?? throw new InvalidOperationException("The inspection fixture photo must pass canonical source validation.");
        var existing = await database.InspectionPhotos.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (existing is not null && (!existing.Matches(scenario.Id, technicianId, 1, photo.PointCode, validated) || existing.AvailableAt is not null))
        {
            return existing.Matches(scenario.Id, technicianId, 1, photo.PointCode, validated);
        }

        if (!seed)
        {
            return false;
        }

        content.Position = 0;
        var result = await photos.UploadAsync(technicianId, scenario.Id, id, 1, photo.PointCode, "image/jpeg", content, cancellationToken);
        return result.Status == InspectionPhotoStatus.Available;
    }

    private async Task<bool> SaveAsync(bool seed, Guid id, Guid technicianId, InspectionDraftSaveRequest request,
        CancellationToken cancellationToken)
    {
        var existing = await database.InspectionDraftSaves.AsNoTracking().SingleOrDefaultAsync(item => item.Id == request.OperationId, cancellationToken);
        if (existing is not null)
        {
            var entries = request.Entries!.Select(entry => new InspectionDraftEntry(entry.PointCode, entry.Kind,
                entry.Severity, entry.EstimatedRepairCost, entry.Notes)).OrderBy(entry => entry.PointCode, StringComparer.Ordinal).ToArray();
            return existing.Receipt().Matches(technicianId, id, request, entries);
        }

        return seed && (await saves.SaveAsync(technicianId, id, request, cancellationToken)).Status == InspectionAssignmentStatus.Succeeded;
    }

    private async Task<bool> SignAsync(bool seed, Guid id, Guid technicianId, InspectionSignRequest request,
        CancellationToken cancellationToken)
    {
        var existing = await database.InspectionSignedRevisions.AsNoTracking().SingleOrDefaultAsync(item => item.Id == request.OperationId, cancellationToken);
        if (existing is not null)
        {
            return existing.Report().Matches(technicianId, id, request);
        }

        return seed && (await signoffs.SignAsync(technicianId, id, request, cancellationToken)).Status == InspectionAssignmentStatus.Succeeded;
    }

    private async Task<bool> RepairAsync(bool seed, Guid id, Guid technicianId, InspectionRepairRequest request,
        CancellationToken cancellationToken)
    {
        var existing = await database.InspectionRepairRequests.AsNoTracking().SingleOrDefaultAsync(item => item.Id == request.OperationId, cancellationToken);
        if (existing is not null)
        {
            return existing.Receipt().Matches(technicianId, id, request, new(request.Items, request.Budget));
        }

        return seed && (await repairs.RequestAsync(technicianId, id, request, cancellationToken)).Status == InspectionAssignmentStatus.Succeeded;
    }

    private async Task<bool> ApproveAsync(bool seed, DevelopmentInspectionScenario scenario, Guid managerId,
        CancellationToken cancellationToken)
    {
        var request = scenario.Approval();
        var existing = await database.InspectionRepairApprovals.AsNoTracking().SingleOrDefaultAsync(item => item.Id == request.OperationId, cancellationToken);
        if (existing is not null)
        {
            return existing.Receipt().Matches(managerId, scenario.Id, request);
        }

        return seed && (await approvals.ApproveAsync(managerId, scenario.Id, request, cancellationToken)).Status == InspectionAssignmentStatus.Succeeded;
    }

    private async Task<bool> CompleteAsync(bool seed, DevelopmentInspectionScenario scenario, Guid technicianId,
        CancellationToken cancellationToken)
    {
        var request = scenario.Completion();
        var existing = await database.InspectionRepairCompletions.AsNoTracking().SingleOrDefaultAsync(item => item.Id == request.OperationId, cancellationToken);
        if (existing is not null)
        {
            return existing.Receipt().Matches(technicianId, scenario.Id, request);
        }

        return seed && (await completions.CompleteAsync(technicianId, scenario.Id, request, cancellationToken)).Status == InspectionAssignmentStatus.Succeeded;
    }

    private async Task<bool> ReopenAsync(bool seed, DevelopmentInspectionScenario scenario, Guid managerId,
        CancellationToken cancellationToken)
    {
        var request = scenario.Reopening();
        var existing = await database.InspectionReopenings.AsNoTracking().SingleOrDefaultAsync(item => item.Id == request.OperationId, cancellationToken);
        if (existing is not null)
        {
            return existing.Receipt().Matches(managerId, scenario.Id, request);
        }

        return seed && (await reopenings.ReopenAsync(managerId, scenario.Id, request, cancellationToken)).Status == InspectionAssignmentStatus.Succeeded;
    }

    private static DevelopmentFixtureReport Report(DevelopmentInspectionScenario scenario, string state, bool attention) =>
        new($"Inspection example ({scenario.Intake.RegistrationPlate}; {scenario.Name})", state, attention);
}

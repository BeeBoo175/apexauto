using Dealership.Application.Accounts;
using Dealership.Application.Inspections;
using Dealership.Domain.Inspections;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Development;

public sealed class DevelopmentInspectionScenarioFixtures(DealershipDbContext database,
    InspectionAssignmentService assignments, InspectionDraftSaveService saves, InspectionSignoffService signoffs,
    InspectionReopeningService reopenings, InspectionRepairRequestService repairs,
    InspectionRepairApprovalService approvals, InspectionRepairCompletionService completions)
{
    public async Task<DevelopmentFixtureReport> RunAsync(bool seed, DevelopmentInspectionScenario scenario,
        Guid vehicleId, Guid technicianId, CancellationToken cancellationToken)
    {
        if (!await PreservesProgressAsync(scenario, technicianId, cancellationToken))
        {
            return Report(scenario, "drift; preserved", true);
        }

        if (!await AssignAsync(seed, scenario, vehicleId, technicianId, cancellationToken) ||
            !await SaveAsync(seed, scenario.Id, technicianId, scenario.Save(), cancellationToken))
        {
            return Report(scenario, "assignment or draft missing/changed; preserved", true);
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
            return Report(scenario, "signed", false);
        }

        if (!await RepairAsync(seed, scenario.Id, technicianId, scenario.Repair(), cancellationToken))
        {
            return Report(scenario, "repair request missing/changed; preserved", true);
        }

        if (scenario.Stage is DevelopmentInspectionStage.RepairApproved or DevelopmentInspectionStage.RepairSuperseded ||
            scenario.Stage == DevelopmentInspectionStage.RepairCompleted && scenario.Budget > 0)
        {
            if (!await ApproveAsync(seed, scenario, managerId, cancellationToken))
            {
                return Report(scenario, "approval missing/changed; preserved", true);
            }
        }

        if (scenario.Stage == DevelopmentInspectionStage.RepairCompleted &&
            !await CompleteAsync(seed, scenario, technicianId, cancellationToken))
        {
            return Report(scenario, "completion missing/changed; preserved", true);
        }

        if (scenario.Stage == DevelopmentInspectionStage.RepairSuperseded &&
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

        var ids = new List<Guid> { scenario.Id, scenario.Save().OperationId };
        if (scenario.Stage != DevelopmentInspectionStage.Draft)
        {
            ids.Add(scenario.Sign().OperationId);
        }
        if (scenario.Stage == DevelopmentInspectionStage.Reinspected)
        {
            ids.AddRange([scenario.Reopening().OperationId, scenario.Save(true).OperationId, scenario.Sign(true).OperationId]);
        }
        if (scenario.Stage is DevelopmentInspectionStage.RepairRequested or DevelopmentInspectionStage.RepairApproved or
            DevelopmentInspectionStage.RepairCompleted or DevelopmentInspectionStage.RepairSuperseded)
        {
            ids.Add(scenario.Repair().OperationId);
        }
        if (scenario.Stage is DevelopmentInspectionStage.RepairApproved or DevelopmentInspectionStage.RepairSuperseded)
        {
            ids.Add(scenario.Approval().OperationId);
        }
        if (scenario.Stage == DevelopmentInspectionStage.RepairCompleted)
        {
            ids.Add(scenario.Completion().OperationId);
        }
        if (scenario.Stage == DevelopmentInspectionStage.RepairSuperseded)
        {
            ids.Add(scenario.Repair(true).OperationId);
        }

        return !await database.InspectionDraftSaves.AnyAsync(item => item.InspectionId == scenario.Id && !ids.Contains(item.Id), cancellationToken) &&
            !await database.InspectionSignedRevisions.AnyAsync(item => item.InspectionId == scenario.Id && !ids.Contains(item.Id), cancellationToken) &&
            !await database.InspectionReopenings.AnyAsync(item => item.InspectionId == scenario.Id && !ids.Contains(item.Id), cancellationToken) &&
            !await database.InspectionRepairRequests.AnyAsync(item => item.InspectionId == scenario.Id && !ids.Contains(item.Id), cancellationToken) &&
            !await database.InspectionRepairApprovals.AnyAsync(item => item.InspectionId == scenario.Id && !ids.Contains(item.Id), cancellationToken) &&
            !await database.InspectionRepairCompletions.AnyAsync(item => item.InspectionId == scenario.Id && !ids.Contains(item.Id), cancellationToken) &&
            !await database.InspectionPhotos.AnyAsync(item => item.InspectionId == scenario.Id, cancellationToken);
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

    private async Task<bool> SaveAsync(bool seed, Guid id, Guid technicianId, InspectionDraftSaveRequest request,
        CancellationToken cancellationToken)
    {
        var existing = await database.InspectionDraftSaves.AsNoTracking().SingleOrDefaultAsync(item => item.Id == request.OperationId, cancellationToken);
        if (existing is not null)
        {
            var entries = request.Entries!.Select(entry => new InspectionDraftEntry(entry.PointCode, entry.Kind,
                entry.Severity, entry.EstimatedRepairCost, entry.Notes)).ToArray();
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

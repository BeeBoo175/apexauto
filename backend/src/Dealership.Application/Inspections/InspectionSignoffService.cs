using Dealership.Application.Accounts;
using Dealership.Domain.Vehicles;

namespace Dealership.Application.Inspections;

public sealed class InspectionSignoffService(IInspectionSignoffStore reports)
{
    public async Task<InspectionReportResult> SignAsync(Guid actorId, Guid inspectionId, InspectionSignRequest request,
        CancellationToken cancellationToken)
    {
        if (actorId == Guid.Empty || inspectionId == Guid.Empty || request.OperationId == Guid.Empty ||
            request.AssignmentVersion < 1 || request.SaveVersion < 1 || request.Revision < 1)
        {
            return new(InspectionAssignmentStatus.Invalid);
        }

        await using var transaction = await reports.LockAsync(actorId, inspectionId, request.OperationId, cancellationToken);
        var context = transaction.Context;
        var actor = context.Actor;
        if (actor is not { IsActive: true, BranchId: not null, Role: AccountRoles.WorkshopTechnician } || !context.BranchActive)
        {
            return new(InspectionAssignmentStatus.Forbidden);
        }

        var vehicle = context.Vehicle;
        var draft = context.Draft;
        if (vehicle is null || draft is null || draft.BranchId != actor.BranchId || draft.TechnicianId != actorId)
        {
            return new(InspectionAssignmentStatus.NotFound);
        }

        var existing = context.Existing;
        if (existing is not null)
        {
            return existing.Matches(actorId, inspectionId, request)
                ? new(InspectionAssignmentStatus.Succeeded, existing) : new(InspectionAssignmentStatus.Conflict);
        }

        var evidence = await transaction.ReadEvidenceAsync(cancellationToken);
        var save = evidence.Save;
        if (draft.IsSigned || draft.Revision != request.Revision || draft.AssignmentVersion != request.AssignmentVersion ||
            vehicle.Status != VehicleStatus.PendingInspection || save is null || save.Version != request.SaveVersion ||
            save.ActorId != actorId || save.AssignmentVersion != request.AssignmentVersion || save.ExaminedPointCodes is null ||
            save.Version <= draft.ReopenedAfterSaveVersion)
        {
            return new(InspectionAssignmentStatus.Conflict);
        }

        var report = InspectionReport.Sign(request.OperationId, draft.Revision, draft, vehicle,
            context.TechnicianIdentity!, save, evidence.Photos, evidence.SignedAt);
        var status = await transaction.CommitAsync(report, cancellationToken);
        return new(status, status == InspectionAssignmentStatus.Succeeded ? report : null);
    }
}

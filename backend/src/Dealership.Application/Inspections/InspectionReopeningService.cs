using Dealership.Application.Accounts;
using Dealership.Domain.Inspections;
using Dealership.Domain.Vehicles;

namespace Dealership.Application.Inspections;

public sealed class InspectionReopeningService(IInspectionReopeningStore reopenings)
{
    public async Task<InspectionReopenResult> ReopenAsync(Guid actorId, Guid inspectionId, InspectionReopenRequest request,
        CancellationToken cancellationToken)
    {
        if (actorId == Guid.Empty || inspectionId == Guid.Empty || request.OperationId == Guid.Empty ||
            request.AssignmentVersion < 1 || request.Revision is < 1 or long.MaxValue)
        {
            return new(InspectionAssignmentStatus.Invalid);
        }

        try
        {
            request = request with { Reason = InspectionAssignment.NormalizeReason(request.Reason) };
        }
        catch (ArgumentException)
        {
            return new(InspectionAssignmentStatus.Invalid);
        }

        if (request.Reason is null)
        {
            return new(InspectionAssignmentStatus.Invalid);
        }

        await using var transaction = await reopenings.LockAsync(actorId, inspectionId, request.OperationId, cancellationToken);
        var context = transaction.Context;
        var actor = context.Actor;
        if (actor is not { IsActive: true, BranchId: not null, Role: AccountRoles.BranchManager } || !context.BranchActive)
        {
            return new(InspectionAssignmentStatus.Forbidden);
        }

        var vehicle = context.Vehicle;
        var draft = context.Draft;
        if (vehicle is null || draft is null || draft.BranchId != actor.BranchId)
        {
            return new(InspectionAssignmentStatus.NotFound);
        }

        var existing = context.Existing;
        if (existing is not null)
        {
            return existing.Matches(actorId, inspectionId, request)
                ? new(InspectionAssignmentStatus.Succeeded, existing) : new(InspectionAssignmentStatus.Conflict);
        }

        if (!draft.IsSigned || draft.Revision != request.Revision || draft.AssignmentVersion != request.AssignmentVersion ||
            vehicle.Status is not (VehicleStatus.PendingInspection or VehicleStatus.Rejected or VehicleStatus.Available))
        {
            return new(InspectionAssignmentStatus.Conflict);
        }

        var evidence = await transaction.ReadEvidenceAsync(cancellationToken);
        var receipt = new InspectionReopenReceipt(request.OperationId, inspectionId, draft.BranchId, actorId,
            evidence.SignedRevisionId, request.AssignmentVersion, draft.Revision, draft.Revision + 1,
            evidence.LatestSaveVersion, request.Reason, evidence.RecordedAt);
        var status = await transaction.CommitAsync(receipt, cancellationToken);
        return new(status, status == InspectionAssignmentStatus.Succeeded ? receipt : null);
    }
}

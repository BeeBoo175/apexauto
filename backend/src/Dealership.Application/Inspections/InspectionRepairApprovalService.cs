using Dealership.Application.Accounts;
using Dealership.Domain.Vehicles;

namespace Dealership.Application.Inspections;

public sealed class InspectionRepairApprovalService(IInspectionRepairApprovalStore approvals)
{
    public async Task<InspectionRepairApprovalResult> ApproveAsync(Guid actorId, Guid inspectionId,
        InspectionRepairApprovalRequest request, CancellationToken cancellationToken)
    {
        if (actorId == Guid.Empty || inspectionId == Guid.Empty || request.OperationId == Guid.Empty ||
            request.RequestId == Guid.Empty || request.AssignmentVersion < 1 || request.Revision < 1 || request.RepairVersion < 1)
        {
            return new(InspectionAssignmentStatus.Invalid);
        }

        await using var transaction = await approvals.LockAsync(actorId, inspectionId, request.OperationId, cancellationToken);
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

        var evidence = await transaction.ReadEvidenceAsync(request.RequestId, cancellationToken);
        var repair = evidence.Request;
        if (!draft.IsSigned || draft.Revision != request.Revision || draft.AssignmentVersion != request.AssignmentVersion ||
            draft.RepairVersion != request.RepairVersion || vehicle.Status != VehicleStatus.PendingInspection ||
            repair is null || repair.InspectionId != inspectionId || repair.Revision != request.Revision ||
            repair.Version != request.RepairVersion || repair.AssignmentVersion != request.AssignmentVersion || evidence.AlreadyApproved)
        {
            return new(InspectionAssignmentStatus.Conflict);
        }

        var now = await transaction.ReadDatabaseTimeAsync(cancellationToken);
        // The immutable request owns approved scope/budget; later requests cannot rewrite this authority.
        var receipt = new InspectionRepairApprovalReceipt(request.OperationId, inspectionId, draft.BranchId, actorId,
            repair.OperationId, draft.AssignmentVersion, draft.Revision, draft.RepairVersion, now);
        var status = await transaction.CommitAsync(receipt, cancellationToken);
        return new(status, status == InspectionAssignmentStatus.Succeeded ? receipt : null);
    }
}

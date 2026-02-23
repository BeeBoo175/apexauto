using Dealership.Application.Accounts;
using Dealership.Domain.Inspections;
using Dealership.Domain.Vehicles;

namespace Dealership.Application.Inspections;

public sealed class InspectionAssignmentService(IInspectionAssignmentStore assignments)
{
    public async Task<InspectionAssignmentResult> AssignAsync(Guid actorId, Guid vehicleId,
        InspectionAssignmentRequest request, CancellationToken cancellationToken)
    {
        if (actorId == Guid.Empty || vehicleId == Guid.Empty || request.OperationId == Guid.Empty ||
            request.TechnicianId == Guid.Empty || request.ExpectedVersion is < 0 or long.MaxValue)
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

        var claim = request.ExpectedVersion == 0;
        if (claim && (request.TechnicianId != actorId || request.Reason is not null) || !claim && request.Reason is null)
        {
            return new(InspectionAssignmentStatus.Invalid);
        }

        await using var transaction = await assignments.LockAsync(actorId, vehicleId, request, cancellationToken);
        var context = transaction.Context;
        var actor = context.Actor;
        var role = claim ? AccountRoles.WorkshopTechnician : AccountRoles.BranchManager;

        if (actor is not { IsActive: true, BranchId: not null } || actor.Role != role || !context.BranchActive)
        {
            return new(InspectionAssignmentStatus.Forbidden);
        }

        var vehicle = context.Vehicle;
        if (vehicle is null || vehicle.BranchId != actor.BranchId)
        {
            return new(InspectionAssignmentStatus.NotFound);
        }

        var existing = context.Existing;
        if (existing is not null)
        {
            var matches = context.ExistingVehicleId == vehicleId && existing.ActorId == actorId &&
                existing.TechnicianId == request.TechnicianId && existing.ExpectedVersion == request.ExpectedVersion &&
                existing.Reason == request.Reason;
            return matches ? Success(existing, vehicleId) : new(InspectionAssignmentStatus.Conflict);
        }

        var technician = context.Technician;
        if (technician is not { IsActive: true, Role: AccountRoles.WorkshopTechnician } ||
            technician.BranchId != vehicle.BranchId || vehicle.Status != VehicleStatus.PendingInspection)
        {
            return new(InspectionAssignmentStatus.Conflict);
        }

        var draft = context.Draft;
        if (claim && draft is not null || !claim && (draft is null || draft.AssignmentVersion != request.ExpectedVersion ||
            draft.TechnicianId == request.TechnicianId || draft.IsSigned))
        {
            return new(InspectionAssignmentStatus.Conflict);
        }

        var now = await transaction.ReadDatabaseTimeAsync(cancellationToken);
        var previous = draft?.TechnicianId;
        if (draft is null)
        {
            draft = new InspectionDraft(request.OperationId, vehicle, request.TechnicianId, now);
        }
        else
        {
            draft.Reassign(request.TechnicianId, request.ExpectedVersion);
        }

        var assignment = new InspectionAssignment(request.OperationId, draft, actorId, previous,
            request.ExpectedVersion, request.Reason, now);
        var status = await transaction.CommitAsync(draft, assignment, cancellationToken);
        return status == InspectionAssignmentStatus.Succeeded ? Success(assignment, vehicleId) : new(status);
    }

    private static InspectionAssignmentResult Success(InspectionAssignment assignment, Guid vehicleId)
    {
        return new(InspectionAssignmentStatus.Succeeded, new InspectionAssignmentReceipt(assignment.Id,
            assignment.InspectionId, vehicleId, assignment.BranchId, assignment.ActorId, assignment.TechnicianId,
            assignment.PreviousTechnicianId, assignment.ExpectedVersion, assignment.Version, assignment.Reason, assignment.RecordedAt));
    }
}

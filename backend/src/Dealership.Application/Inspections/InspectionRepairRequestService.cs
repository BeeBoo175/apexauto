using Dealership.Application.Accounts;
using Dealership.Domain.Inspections;
using Dealership.Domain.Vehicles;

namespace Dealership.Application.Inspections;

public sealed class InspectionRepairRequestService(IInspectionRepairRequestStore repairs)
{
    public async Task<InspectionRepairResult> RequestAsync(Guid actorId, Guid inspectionId, InspectionRepairRequest request,
        CancellationToken cancellationToken)
    {
        if (actorId == Guid.Empty || inspectionId == Guid.Empty || request.OperationId == Guid.Empty ||
            request.AssignmentVersion < 1 || request.Revision < 1 || request.ExpectedVersion is < 0 or long.MaxValue)
        {
            return new(InspectionAssignmentStatus.Invalid);
        }

        InspectionRepairScope scope;
        try
        {
            scope = new(request.Items, request.Budget);
        }
        catch (ArgumentException)
        {
            return new(InspectionAssignmentStatus.Invalid);
        }

        await using var transaction = await repairs.LockAsync(actorId, inspectionId, request.OperationId, cancellationToken);
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
            return existing.Matches(actorId, inspectionId, request, scope)
                ? new(InspectionAssignmentStatus.Succeeded, existing) : new(InspectionAssignmentStatus.Conflict);
        }

        if (!draft.IsSigned || draft.Revision != request.Revision || draft.AssignmentVersion != request.AssignmentVersion ||
            draft.RepairVersion != request.ExpectedVersion ||
            vehicle.Status is not (VehicleStatus.PendingInspection or VehicleStatus.Available))
        {
            return new(InspectionAssignmentStatus.Conflict);
        }

        var report = await transaction.ReadReportAsync(cancellationToken);
        if (report.Assessment.IsRejected)
        {
            return new(InspectionAssignmentStatus.Conflict);
        }

        var findings = report.Checklist.Where(point => point.Kind == InspectionEntryKind.Finding)
            .Select(point => point.Code).ToHashSet(StringComparer.Ordinal);
        if (scope.Items.Any(item => !findings.Contains(item.PointCode)))
        {
            return new(InspectionAssignmentStatus.Invalid);
        }

        var now = await transaction.ReadDatabaseTimeAsync(cancellationToken);
        var receipt = new InspectionRepairReceipt(request.OperationId, inspectionId, draft.BranchId, actorId,
            report.OperationId, draft.AssignmentVersion, draft.Revision, request.ExpectedVersion, request.ExpectedVersion + 1,
            scope.Items, scope.Budget, now);
        var status = await transaction.CommitAsync(receipt, cancellationToken);
        return new(status, status == InspectionAssignmentStatus.Succeeded ? receipt : null);
    }
}

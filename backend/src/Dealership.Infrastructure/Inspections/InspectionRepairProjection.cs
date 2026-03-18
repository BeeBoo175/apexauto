using Dealership.Application.Inspections;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Inspections;

internal static class InspectionRepairProjection
{
    public static IQueryable<Row> Read(DealershipDbContext database,
        IQueryable<InspectionRepairRequestRecord> requests, Guid? technicianId)
    {
        var covered = InspectionRepairAuthority.CoveredRequests(database);
        var rows = from request in requests
                   join draft in database.InspectionDrafts.AsNoTracking() on request.InspectionId equals draft.Id
                   join vehicle in database.Vehicles.AsNoTracking() on draft.VehicleId equals vehicle.Id
                   join branch in database.Branches.AsNoTracking() on request.BranchId equals branch.Id
                   join approved in database.InspectionRepairApprovals.AsNoTracking() on request.Id equals approved.RequestId into approvals
                   from approval in approvals.DefaultIfEmpty()
                   join completed in database.InspectionRepairCompletions.AsNoTracking() on request.Id equals completed.RequestId into completions
                   from completion in completions.DefaultIfEmpty()
                   from covering in covered.Where(item => item.Request.Id == request.Id)
                       .OrderByDescending(item => item.Approval.ApprovedAt).ThenByDescending(item => item.Approval.Id)
                       .Select(item => item.Approval).Take(1).DefaultIfEmpty()
                   where technicianId == null || draft.TechnicianId == technicianId
                   select new
                   {
                       Request = request,
                       Draft = draft,
                       Vehicle = vehicle,
                       BranchName = branch.Name,
                       ApprovalId = (Guid?)approval.Id,
                       CoveringApprovalId = (Guid?)covering.Id,
                       CompletionId = (Guid?)completion.Id,
                       ApprovalState = covering == null ? InspectionRepairQueueStatus.PendingApproval : InspectionRepairQueueStatus.Approved
                   };
        var priced = rows.Select(row => new
        {
            row.Request,
            row.Draft,
            row.Vehicle,
            row.BranchName,
            row.ApprovalId,
            row.CoveringApprovalId,
            row.CompletionId,
            State = row.Request.Budget == 0 ? InspectionRepairQueueStatus.ApprovalNotRequired : row.ApprovalState
        });
        var completedWork = priced.Select(row => new
        {
            row.Request,
            row.Draft,
            row.Vehicle,
            row.BranchName,
            row.ApprovalId,
            row.CoveringApprovalId,
            row.CompletionId,
            State = row.CompletionId != null ? InspectionRepairQueueStatus.Completed : row.State
        });
        // Retained approval evidence cannot grant actionability to an old request or reopened revision.
        return completedWork.Select(row => new Row
        {
            Request = row.Request,
            Vehicle = row.Vehicle,
            BranchName = row.BranchName,
            ApprovalId = row.ApprovalId,
            CoveringApprovalId = row.CoveringApprovalId,
            CompletionId = row.CompletionId,
            TechnicianId = row.Draft.TechnicianId,
            State = !row.Draft.IsSigned || row.Draft.Revision != row.Request.Revision ||
                row.Draft.AssignmentVersion != row.Request.AssignmentVersion || row.Draft.RepairVersion != row.Request.Version ||
                row.Vehicle.Status == VehicleStatus.Rejected ? InspectionRepairQueueStatus.Superseded : row.State
        });
    }

    public static InspectionRepairQueueItem Item(Row row) =>
        new(row.Request.Id, row.Request.InspectionId, row.Vehicle.Id, row.Request.BranchId,
            row.BranchName, row.Vehicle.RegistrationPlate, row.TechnicianId, row.Request.AssignmentVersion,
            row.Request.Revision, row.Request.Version, row.Request.Budget, row.Request.RequestedAt,
            row.ApprovalId, row.CoveringApprovalId, row.State, row.CompletionId);

    internal sealed class Row
    {
        public required InspectionRepairRequestRecord Request { get; init; }
        public required Vehicle Vehicle { get; init; }
        public required string BranchName { get; init; }
        public Guid? ApprovalId { get; init; }
        public Guid? CoveringApprovalId { get; init; }
        public Guid? CompletionId { get; init; }
        public Guid TechnicianId { get; init; }
        public InspectionRepairQueueStatus State { get; init; }
    }
}

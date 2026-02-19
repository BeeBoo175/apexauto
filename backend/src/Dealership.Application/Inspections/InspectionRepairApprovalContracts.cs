namespace Dealership.Application.Inspections;

public sealed record InspectionRepairApprovalRequest(Guid OperationId, Guid RequestId, long AssignmentVersion,
    long Revision, long RepairVersion);

public sealed record InspectionRepairApprovalReceipt(Guid OperationId, Guid InspectionId, Guid BranchId, Guid ActorId,
    Guid RequestId, long AssignmentVersion, long Revision, long RepairVersion, DateTimeOffset ApprovedAt);

public sealed record InspectionRepairApprovalResult(InspectionAssignmentStatus Status,
    InspectionRepairApprovalReceipt? Receipt = null);

public interface IInspectionRepairApprovalStore
{
    Task<InspectionRepairApprovalResult> ApproveAsync(Guid actorId, Guid inspectionId,
        InspectionRepairApprovalRequest request, CancellationToken cancellationToken);

    Task<InspectionRepairApprovalResult> ReadAsync(Guid actorId, Guid inspectionId, long revision,
        CancellationToken cancellationToken);
}

namespace Dealership.Application.Inspections;

public sealed record InspectionRepairCompletionRequest(Guid OperationId, Guid RequestId,
    long AssignmentVersion, long Revision, long RepairVersion, string? Notes);

public sealed record InspectionRepairCompletionReceipt(Guid OperationId, Guid InspectionId, Guid BranchId,
    Guid ActorId, Guid RequestId, Guid? ApprovalId, long AssignmentVersion, long Revision, long RepairVersion,
    string Notes, DateTimeOffset CompletedAt);

public sealed record InspectionRepairCompletionResult(InspectionAssignmentStatus Status,
    InspectionRepairCompletionReceipt? Receipt = null);

public interface IInspectionRepairCompletionStore
{
    Task<InspectionRepairCompletionResult> CompleteAsync(Guid actorId, Guid inspectionId,
        InspectionRepairCompletionRequest request, CancellationToken cancellationToken);
}

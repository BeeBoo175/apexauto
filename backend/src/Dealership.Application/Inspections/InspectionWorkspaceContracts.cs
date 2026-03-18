namespace Dealership.Application.Inspections;

public sealed record InspectionWorkspace(InspectionDraftSnapshot Draft, InspectionReport? Report,
    InspectionRepairReceipt? CurrentRequest, InspectionRepairQueueItem? RepairState);

public sealed record InspectionWorkspaceResult(InspectionAssignmentStatus Status, InspectionWorkspace? Workspace = null);

public interface IInspectionWorkspaceStore
{
    Task<InspectionWorkspaceResult> ReadAsync(Guid actorId, Guid inspectionId, CancellationToken cancellationToken);
}

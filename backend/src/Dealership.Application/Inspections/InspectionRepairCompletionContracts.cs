using Dealership.Domain.Inspections;
using Dealership.Domain.Vehicles;

namespace Dealership.Application.Inspections;

public sealed record InspectionRepairCompletionRequest(Guid OperationId, Guid RequestId,
    long AssignmentVersion, long Revision, long RepairVersion, string? Notes);

public sealed record InspectionRepairCompletionReceipt(Guid OperationId, Guid InspectionId, Guid BranchId,
    Guid ActorId, Guid RequestId, Guid? ApprovalId, long AssignmentVersion, long Revision, long RepairVersion,
    string Notes, DateTimeOffset CompletedAt)
{
    public bool Matches(Guid actorId, Guid inspectionId, InspectionRepairCompletionRequest request) =>
        ActorId == actorId && InspectionId == inspectionId && RequestId == request.RequestId &&
        AssignmentVersion == request.AssignmentVersion && Revision == request.Revision &&
        RepairVersion == request.RepairVersion && Notes == request.Notes;
}

public sealed record InspectionRepairCompletionResult(InspectionAssignmentStatus Status,
    InspectionRepairCompletionReceipt? Receipt = null);

public sealed record InspectionRepairCompletionContext(InspectionStaff? Actor, bool BranchActive, Vehicle? Vehicle,
    InspectionDraft? Draft, InspectionRepairCompletionReceipt? Existing);

public sealed record InspectionRepairCompletionEvidence(InspectionRepairReceipt? Request, bool AlreadyCompleted);

public sealed record InspectionRepairApprovalLookup(Guid InspectionId, long Revision,
    IReadOnlyList<InspectionRepairItem> ExactItems, decimal MinimumBudget);

public interface IInspectionRepairCompletionTransaction : IAsyncDisposable
{
    InspectionRepairCompletionContext Context { get; }

    Task<InspectionRepairCompletionEvidence> ReadEvidenceAsync(Guid requestId, CancellationToken cancellationToken);

    Task<InspectionRepairApprovalReceipt?> ReadLatestApprovalAsync(InspectionRepairApprovalLookup lookup,
        CancellationToken cancellationToken);

    Task<DateTimeOffset> ReadDatabaseTimeAsync(CancellationToken cancellationToken);

    Task<InspectionAssignmentStatus> CommitAsync(InspectionRepairCompletionReceipt receipt, CancellationToken cancellationToken);
}

public interface IInspectionRepairCompletionStore
{
    Task<IInspectionRepairCompletionTransaction> LockAsync(Guid actorId, Guid inspectionId, Guid operationId,
        CancellationToken cancellationToken);
}

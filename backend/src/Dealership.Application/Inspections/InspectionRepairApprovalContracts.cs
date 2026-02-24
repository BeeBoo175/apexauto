using Dealership.Domain.Inspections;
using Dealership.Domain.Vehicles;

namespace Dealership.Application.Inspections;

public sealed record InspectionRepairApprovalRequest(Guid OperationId, Guid RequestId, long AssignmentVersion,
    long Revision, long RepairVersion);

public sealed record InspectionRepairApprovalReceipt(Guid OperationId, Guid InspectionId, Guid BranchId, Guid ActorId,
    Guid RequestId, long AssignmentVersion, long Revision, long RepairVersion, DateTimeOffset ApprovedAt)
{
    public bool Matches(Guid actorId, Guid inspectionId, InspectionRepairApprovalRequest request) =>
        ActorId == actorId && InspectionId == inspectionId && RequestId == request.RequestId &&
        AssignmentVersion == request.AssignmentVersion && Revision == request.Revision && RepairVersion == request.RepairVersion;
}

public sealed record InspectionRepairApprovalResult(InspectionAssignmentStatus Status,
    InspectionRepairApprovalReceipt? Receipt = null);

public sealed record InspectionRepairApprovalContext(InspectionStaff? Actor, bool BranchActive, Vehicle? Vehicle,
    InspectionDraft? Draft, InspectionRepairApprovalReceipt? Existing);

public sealed record InspectionRepairApprovalEvidence(InspectionRepairReceipt? Request, bool AlreadyApproved);

public interface IInspectionRepairApprovalTransaction : IAsyncDisposable
{
    InspectionRepairApprovalContext Context { get; }

    Task<InspectionRepairApprovalEvidence> ReadEvidenceAsync(Guid requestId, CancellationToken cancellationToken);

    Task<DateTimeOffset> ReadDatabaseTimeAsync(CancellationToken cancellationToken);

    Task<InspectionAssignmentStatus> CommitAsync(InspectionRepairApprovalReceipt receipt, CancellationToken cancellationToken);
}

public interface IInspectionRepairApprovalStore
{
    Task<IInspectionRepairApprovalTransaction> LockAsync(Guid actorId, Guid inspectionId, Guid operationId,
        CancellationToken cancellationToken);

    Task<InspectionRepairApprovalResult> ReadAsync(Guid actorId, Guid inspectionId, long revision,
        CancellationToken cancellationToken);
}

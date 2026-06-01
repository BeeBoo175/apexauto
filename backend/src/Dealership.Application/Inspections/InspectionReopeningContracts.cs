using Dealership.Domain.Inspections;
using Dealership.Domain.Vehicles;

namespace Dealership.Application.Inspections;

public sealed record InspectionReopenRequest(Guid OperationId, long AssignmentVersion, long Revision, string? Reason);

public sealed record InspectionReopenReceipt(Guid OperationId, Guid InspectionId, Guid BranchId, Guid ActorId,
    Guid SignedRevisionId, long AssignmentVersion, long ExpectedRevision, long Revision,
    long AfterSaveVersion, string Reason, DateTimeOffset RecordedAt)
{
    public bool Matches(Guid actorId, Guid inspectionId, InspectionReopenRequest request) =>
        ActorId == actorId && InspectionId == inspectionId && AssignmentVersion == request.AssignmentVersion &&
        ExpectedRevision == request.Revision && Reason == request.Reason;
}

public sealed record InspectionReopenResult(InspectionAssignmentStatus Status, InspectionReopenReceipt? Receipt = null);

public sealed record InspectionReopeningContext(InspectionStaff? Actor, bool BranchActive, Vehicle? Vehicle,
    InspectionDraft? Draft, InspectionReopenReceipt? Existing, bool VehicleOccupied = false);

public sealed record InspectionReopeningEvidence(Guid SignedRevisionId, long LatestSaveVersion, DateTimeOffset RecordedAt);

public interface IInspectionReopeningTransaction : IAsyncDisposable
{
    InspectionReopeningContext Context { get; }

    Task<InspectionReopeningEvidence> ReadEvidenceAsync(CancellationToken cancellationToken);

    Task<InspectionAssignmentStatus> CommitAsync(InspectionReopenReceipt receipt, CancellationToken cancellationToken);
}

public interface IInspectionReopeningStore
{
    Task<IInspectionReopeningTransaction> LockAsync(Guid actorId, Guid inspectionId, Guid operationId,
        CancellationToken cancellationToken);
}

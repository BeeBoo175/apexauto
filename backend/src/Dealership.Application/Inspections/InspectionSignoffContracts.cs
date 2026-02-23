using Dealership.Domain.Inspections;
using Dealership.Domain.Vehicles;

namespace Dealership.Application.Inspections;

public sealed record InspectionSignoffContext(InspectionStaff? Actor, bool BranchActive, string? TechnicianIdentity,
    Vehicle? Vehicle, InspectionDraft? Draft, InspectionReport? Existing);

public sealed record InspectionSignoffEvidence(InspectionDraftSaveReceipt? Save,
    IReadOnlyList<InspectionPhoto> Photos, DateTimeOffset SignedAt);

public interface IInspectionSignoffTransaction : IAsyncDisposable
{
    InspectionSignoffContext Context { get; }

    Task<InspectionSignoffEvidence> ReadEvidenceAsync(CancellationToken cancellationToken);

    Task<InspectionAssignmentStatus> CommitAsync(InspectionReport report, CancellationToken cancellationToken);
}

public interface IInspectionSignoffStore
{
    Task<IInspectionSignoffTransaction> LockAsync(Guid actorId, Guid inspectionId, Guid operationId,
        CancellationToken cancellationToken);
}

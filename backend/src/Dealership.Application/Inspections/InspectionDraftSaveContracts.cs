using Dealership.Domain.Inspections;
using Dealership.Domain.Vehicles;
using System.Text.Json.Serialization;
using Dealership.Application.Common;

namespace Dealership.Application.Inspections;

public sealed record InspectionDraftSaveRequest(Guid OperationId, long AssignmentVersion, long ExpectedVersion,
    IReadOnlyList<InspectionDraftEntryInput>? Entries)
{
    public IReadOnlyList<Guid>? PhotoIds { get; init; } = [];
    // Null leaves this save unattested; later edits must attest again explicitly.
    public IReadOnlyList<string>? ExaminedPointCodes { get; init; }
}

public sealed record InspectionDraftEntryInput(string PointCode, InspectionEntryKind Kind, InspectionSeverity? Severity,
    [property: JsonConverter(typeof(MyrAmountJsonConverter))] decimal EstimatedRepairCost, string Notes);

public sealed record InspectionDraftSaveReceipt(Guid OperationId, Guid InspectionId, Guid ActorId,
    long AssignmentVersion, long ExpectedVersion, long Version, string CatalogVersion,
    IReadOnlyList<InspectionDraftEntry> Entries, InspectionDraftAssessment Assessment, DateTimeOffset RecordedAt)
{
    public IReadOnlyList<Guid> PhotoIds { get; init; } = [];
    public IReadOnlyList<string>? ExaminedPointCodes { get; init; }

    public bool Matches(Guid actorId, Guid inspectionId, InspectionDraftSaveRequest request,
        IReadOnlyList<InspectionDraftEntry> entries)
    {
        return ActorId == actorId && InspectionId == inspectionId && AssignmentVersion == request.AssignmentVersion &&
            ExpectedVersion == request.ExpectedVersion && Entries.SequenceEqual(entries) &&
            PhotoIds.SequenceEqual(request.PhotoIds!.Order()) && MatchesAttestation(ExaminedPointCodes, request.ExaminedPointCodes);
    }

    private static bool MatchesAttestation(IReadOnlyList<string>? saved, IReadOnlyList<string>? requested)
    {
        if (saved is null || requested is null)
        {
            return saved is null && requested is null;
        }

        return saved.SequenceEqual(requested.Order(StringComparer.Ordinal));
    }
}

public sealed record InspectionDraftAssessment(decimal EstimatedRepairCost, InspectionGrade? Grade, bool IsRejected);

public sealed record InspectionDraftSaveResult(InspectionAssignmentStatus Status, InspectionDraftSaveReceipt? Receipt = null);

public sealed record InspectionDraftSaveContext(InspectionStaff? Actor, bool BranchActive, Vehicle? Vehicle,
    InspectionDraft? Draft, InspectionDraftSaveReceipt? Existing, long Version);

public sealed record InspectionSavePhoto(Guid Id, Guid InspectionId, string PointCode, DateTimeOffset ExpiresAt,
    DateTimeOffset? AvailableAt, bool Retained, bool RemovalPending);

public sealed record InspectionSavePhotoSet(IReadOnlyList<InspectionSavePhoto> Photos, DateTimeOffset RecordedAt);

public interface IInspectionDraftSaveTransaction : IAsyncDisposable
{
    InspectionDraftSaveContext Context { get; }

    Task<InspectionSavePhotoSet> LockPhotosAsync(IReadOnlyList<Guid> photoIds, CancellationToken cancellationToken);

    Task<InspectionAssignmentStatus> CommitAsync(InspectionDraftSaveReceipt receipt, IReadOnlyList<InspectionSavePhoto> photos,
        CancellationToken cancellationToken);
}

public interface IInspectionDraftSaveStore
{
    Task<IInspectionDraftSaveTransaction> LockAsync(Guid actorId, Guid inspectionId, Guid operationId,
        CancellationToken cancellationToken);
}

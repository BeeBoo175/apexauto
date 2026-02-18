using Dealership.Domain.Inspections;
using System.Text.Json.Serialization;

namespace Dealership.Application.Inspections;

public sealed record InspectionDraftSaveRequest(Guid OperationId, long AssignmentVersion, long ExpectedVersion,
    IReadOnlyList<InspectionDraftEntryInput>? Entries)
{
    public IReadOnlyList<Guid>? PhotoIds { get; init; } = [];
    // Null leaves this save unattested; later edits must attest again explicitly.
    public IReadOnlyList<string>? ExaminedPointCodes { get; init; }
}

public sealed record InspectionDraftEntryInput(string PointCode, InspectionEntryKind Kind, InspectionSeverity? Severity,
    [property: JsonConverter(typeof(InspectionEstimateJsonConverter))] decimal EstimatedRepairCost, string Notes);

public sealed record InspectionDraftSaveReceipt(Guid OperationId, Guid InspectionId, Guid ActorId,
    long AssignmentVersion, long ExpectedVersion, long Version, string CatalogVersion,
    IReadOnlyList<InspectionDraftEntry> Entries, InspectionDraftAssessment Assessment, DateTimeOffset RecordedAt)
{
    public IReadOnlyList<Guid> PhotoIds { get; init; } = [];
    public IReadOnlyList<string>? ExaminedPointCodes { get; init; }
}

public sealed record InspectionDraftAssessment(decimal EstimatedRepairCost, InspectionGrade? Grade, bool IsRejected);

public sealed record InspectionDraftSaveResult(InspectionAssignmentStatus Status, InspectionDraftSaveReceipt? Receipt = null);

public interface IInspectionDraftSaveStore
{
    Task<InspectionDraftSaveResult> SaveAsync(Guid actorId, Guid inspectionId, InspectionDraftSaveRequest request,
        CancellationToken cancellationToken);
}

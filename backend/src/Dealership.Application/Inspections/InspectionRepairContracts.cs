using System.Text.Json.Serialization;
using Dealership.Domain.Inspections;

namespace Dealership.Application.Inspections;

public sealed record InspectionRepairRequest(Guid OperationId, long AssignmentVersion, long Revision,
    long ExpectedVersion, IReadOnlyList<InspectionRepairItem> Items,
    [property: JsonConverter(typeof(InspectionEstimateJsonConverter))] decimal Budget);

public sealed record InspectionRepairReceipt(Guid OperationId, Guid InspectionId, Guid BranchId, Guid ActorId,
    Guid SignedRevisionId, long AssignmentVersion, long Revision, long ExpectedVersion, long Version,
    IReadOnlyList<InspectionRepairItem> Items, decimal Budget, DateTimeOffset RequestedAt);

public sealed record InspectionRepairResult(InspectionAssignmentStatus Status, InspectionRepairReceipt? Receipt = null);

public interface IInspectionRepairRequestStore
{
    Task<InspectionRepairResult> RequestAsync(Guid actorId, Guid inspectionId, InspectionRepairRequest request,
        CancellationToken cancellationToken);

    Task<InspectionRepairResult> ReadAsync(Guid actorId, Guid inspectionId, long revision,
        CancellationToken cancellationToken);
}

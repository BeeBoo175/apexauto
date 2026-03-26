using System.Text.Json.Serialization;
using Dealership.Application.Common;
using Dealership.Domain.Inspections;
using Dealership.Domain.Vehicles;

namespace Dealership.Application.Inspections;

public sealed record InspectionRepairRequest(Guid OperationId, long AssignmentVersion, long Revision,
    long ExpectedVersion, IReadOnlyList<InspectionRepairItem> Items,
    [property: JsonConverter(typeof(MyrAmountJsonConverter))] decimal Budget);

public sealed record InspectionRepairReceipt(Guid OperationId, Guid InspectionId, Guid BranchId, Guid ActorId,
    Guid SignedRevisionId, long AssignmentVersion, long Revision, long ExpectedVersion, long Version,
    IReadOnlyList<InspectionRepairItem> Items, decimal Budget, DateTimeOffset RequestedAt)
{
    public bool Matches(Guid actorId, Guid inspectionId, InspectionRepairRequest request, InspectionRepairScope scope) =>
        ActorId == actorId && InspectionId == inspectionId && AssignmentVersion == request.AssignmentVersion &&
        Revision == request.Revision && ExpectedVersion == request.ExpectedVersion && Budget == scope.Budget &&
        Items.SequenceEqual(scope.Items);
}

public sealed record InspectionRepairResult(InspectionAssignmentStatus Status, InspectionRepairReceipt? Receipt = null);

public sealed record InspectionRepairRequestContext(InspectionStaff? Actor, bool BranchActive, Vehicle? Vehicle,
    InspectionDraft? Draft, InspectionRepairReceipt? Existing);

public interface IInspectionRepairRequestTransaction : IAsyncDisposable
{
    InspectionRepairRequestContext Context { get; }

    Task<InspectionReport> ReadReportAsync(CancellationToken cancellationToken);

    Task<DateTimeOffset> ReadDatabaseTimeAsync(CancellationToken cancellationToken);

    Task<InspectionAssignmentStatus> CommitAsync(InspectionRepairReceipt receipt, CancellationToken cancellationToken);
}

public interface IInspectionRepairRequestStore
{
    Task<IInspectionRepairRequestTransaction> LockAsync(Guid actorId, Guid inspectionId, Guid operationId,
        CancellationToken cancellationToken);

    Task<InspectionRepairResult> ReadAsync(Guid actorId, Guid inspectionId, long revision,
        CancellationToken cancellationToken);
}

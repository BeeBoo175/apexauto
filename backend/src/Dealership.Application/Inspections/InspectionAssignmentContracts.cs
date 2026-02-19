namespace Dealership.Application.Inspections;

public sealed record InspectionAssignmentRequest(Guid OperationId, Guid TechnicianId, long ExpectedVersion, string? Reason);

public sealed record InspectionDraftSnapshot(Guid Id, Guid VehicleId, Guid BranchId, Guid TechnicianId,
    long AssignmentVersion, DateTimeOffset CreatedAt, InspectionDraftSaveReceipt? LatestSave)
{
    public long Revision { get; init; } = 1;
    public bool IsSigned { get; init; }
    public long ReopenedAfterSaveVersion { get; init; }
}

public sealed record InspectionAssignmentReceipt(Guid OperationId, Guid InspectionId, Guid VehicleId, Guid BranchId,
    Guid ActorId, Guid TechnicianId, Guid? PreviousTechnicianId, long ExpectedVersion, long Version,
    string? Reason, DateTimeOffset RecordedAt);

public enum InspectionAssignmentStatus
{
    Succeeded,
    Invalid,
    Forbidden,
    NotFound,
    Conflict
}

public sealed record InspectionAssignmentResult(InspectionAssignmentStatus Status, InspectionAssignmentReceipt? Receipt = null);

public sealed record InspectionDraftResult(InspectionAssignmentStatus Status, InspectionDraftSnapshot? Draft = null);

public interface IInspectionAssignmentStore
{
    Task<InspectionAssignmentResult> AssignAsync(Guid actorId, Guid vehicleId, InspectionAssignmentRequest request,
        CancellationToken cancellationToken);

    Task<InspectionDraftResult> ReadAsync(Guid actorId, Guid inspectionId, CancellationToken cancellationToken);
}

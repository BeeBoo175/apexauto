using Dealership.Domain.Inspections;
using Dealership.Domain.Vehicles;

namespace Dealership.Application.Inspections;

public sealed record InspectionAssignmentRequest(Guid OperationId, Guid TechnicianId, long ExpectedVersion, string? Reason);

public sealed record InspectionDraftSnapshot(Guid Id, Guid VehicleId, Guid BranchId, Guid TechnicianId,
    long AssignmentVersion, DateTimeOffset CreatedAt, InspectionDraftSaveReceipt? LatestSave)
{
    public long Revision { get; init; } = 1;
    public bool IsSigned { get; init; }
    public long ReopenedAfterSaveVersion { get; init; }
    public long RepairVersion { get; init; }
    public string CatalogVersion { get; init; } = InspectionCatalog.Version;
    public IReadOnlyList<InspectionPoint> Catalog { get; init; } = InspectionCatalog.Points;
    public InspectionDraftVehicle? Vehicle { get; init; }
    public IReadOnlyList<InspectionPhotoReference> PhotoReferences { get; init; } = [];
}

public sealed record InspectionPhotoReference(Guid Id, string PointCode);

public sealed record InspectionDraftVehicle(string RegistrationPlate, string Make, string Model, string Vin,
    int MileageKilometres, string Status, string BranchName);

public sealed record InspectionTechnician(Guid Id, string Identity);

public sealed record InspectionTechnicianPage(IReadOnlyList<InspectionTechnician> Items, int Total, int Page, int PageSize);

public sealed record InspectionTechnicianResult(InspectionAssignmentStatus Status, InspectionTechnicianPage? Technicians = null);

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

public sealed record InspectionStaff(bool IsActive, string Role, Guid? BranchId);

public sealed record InspectionAssignmentContext(InspectionStaff? Actor, bool BranchActive, Vehicle? Vehicle,
    InspectionStaff? Technician, InspectionAssignment? Existing, Guid? ExistingVehicleId, InspectionDraft? Draft);

public interface IInspectionAssignmentTransaction : IAsyncDisposable
{
    InspectionAssignmentContext Context { get; }

    Task<DateTimeOffset> ReadDatabaseTimeAsync(CancellationToken cancellationToken);

    Task<InspectionAssignmentStatus> CommitAsync(InspectionDraft draft, InspectionAssignment assignment,
        CancellationToken cancellationToken);
}

public interface IInspectionAssignmentStore
{
    Task<InspectionTechnicianResult> TechniciansAsync(Guid actorId, int page, int pageSize, CancellationToken cancellationToken);

    Task<IInspectionAssignmentTransaction> LockAsync(Guid actorId, Guid vehicleId, InspectionAssignmentRequest request,
        CancellationToken cancellationToken);

    Task<InspectionDraftResult> ReadAsync(Guid actorId, Guid inspectionId, CancellationToken cancellationToken);
}

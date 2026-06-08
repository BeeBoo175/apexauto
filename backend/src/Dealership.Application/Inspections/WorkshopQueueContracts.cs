namespace Dealership.Application.Inspections;

public enum WorkshopTask
{
    Active,
    Inspection,
    Approval,
    Repairs,
    All,
    MyWork,
    Waiting,
    Claimable
}

public enum WorkshopWorkStatus
{
    AwaitingInspection,
    InspectionUnderway,
    RepairRequestRequired,
    ApprovalRequired,
    RepairsToComplete,
    ReadyForSale,
    Rejected
}

public sealed record WorkshopQueueItem(Guid VehicleId, Guid BranchId, string BranchName,
    string RegistrationPlate, string Make, string Model, string VehicleStatus,
    Guid? InspectionId, Guid? TechnicianId, string? TechnicianIdentity, long? Revision,
    Guid? RequestId, decimal? Budget, DateTimeOffset Since, string DateSource, WorkshopWorkStatus Status,
    int ManufactureYear = 0, string Trim = "", DateTimeOffset CheckedInAt = default,
    DateTimeOffset? RequestedAt = null);

// Managerial task totals for the actor's branch scope, independent of dates, search and column filters.
public sealed record WorkshopQueueCounts(int Active, int Inspection, int Approval, int Repairs, int All);

public sealed record WorkshopQueuePage(IReadOnlyList<WorkshopQueueItem> Items, int Total, int Page, int PageSize,
    WorkshopQueueCounts? Counts = null);

public sealed record WorkshopQueueResult(InspectionAssignmentStatus Status, WorkshopQueuePage? Queue = null);

public sealed record WorkshopJobs(IReadOnlyList<WorkshopQueueItem> Items, int Total, int Page, int PageSize);

public sealed record WorkshopJobsResult(InspectionAssignmentStatus Status, WorkshopJobs? Queue = null);

public interface IWorkshopQueueStore
{
    Task<WorkshopQueueResult> ReadAsync(Guid actorId, InspectionQueueFilter filter,
        WorkshopTask task, CancellationToken cancellationToken);

    Task<WorkshopJobsResult> ReadJobsAsync(Guid actorId, InspectionQueueFilter filter,
        WorkshopTask task, CancellationToken cancellationToken);
}

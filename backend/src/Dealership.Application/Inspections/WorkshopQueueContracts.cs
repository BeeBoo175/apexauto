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
    Guid? RequestId, decimal? Budget, DateTimeOffset Since, string DateSource, WorkshopWorkStatus Status);

public sealed record WorkshopQueuePage(IReadOnlyList<WorkshopQueueItem> Items, int Total, int Page, int PageSize);

public sealed record WorkshopQueueResult(InspectionAssignmentStatus Status, WorkshopQueuePage? Queue = null);

public interface IWorkshopQueueStore
{
    Task<WorkshopQueueResult> ReadAsync(Guid actorId, InspectionQueueFilter filter,
        WorkshopTask task, CancellationToken cancellationToken);
}

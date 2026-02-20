using Dealership.Application.Common;

namespace Dealership.Application.Inspections;

public enum InspectionQueueStatus
{
    All,
    Unassigned,
    Draft,
    Sealed
}

public enum InspectionRepairQueueStatus
{
    All,
    PendingApproval,
    Approved,
    ApprovalNotRequired,
    Completed,
    Superseded
}

public sealed record InspectionQueueFilter(DateTimeOffset? From = null, DateTimeOffset? Until = null,
    Guid? BranchId = null, int Page = 1, int PageSize = Pagination.DefaultPageSize, string Search = "")
{
    public bool IsValid => Pagination.IsValid(Page, PageSize) && BranchId != Guid.Empty &&
        ListSearch.IsValid(Search) && ListDateRange.IsValid(From, Until);

    public static InspectionQueueFilter? Parse(string? from, string? to, Guid? branchId,
        int page = 1, int pageSize = Pagination.DefaultPageSize, string? search = null)
    {
        var dates = ListDateRange.Parse(from, to);
        if (dates is null)
        {
            return null;
        }

        var filter = new InspectionQueueFilter(dates.From, dates.Until, branchId, page, pageSize, search?.Trim() ?? "");
        return filter.IsValid ? filter : null;
    }
}

public sealed record InspectionQueueItem(Guid VehicleId, Guid BranchId, string BranchName, string RegistrationPlate,
    string Make, string Model, DateTimeOffset CheckedInAt, string VehicleStatus, Guid? InspectionId, Guid? TechnicianId,
    long? AssignmentVersion, long? Revision, InspectionQueueStatus Status);

public sealed record InspectionRepairQueueItem(Guid RequestId, Guid InspectionId, Guid VehicleId, Guid BranchId,
    string BranchName, string RegistrationPlate, Guid TechnicianId, long AssignmentVersion, long Revision,
    long RepairVersion, decimal Budget, DateTimeOffset RequestedAt, Guid? ApprovalId, Guid? CoveringApprovalId,
    InspectionRepairQueueStatus Status, Guid? CompletionId);

public sealed record InspectionQueuePage(IReadOnlyList<InspectionQueueItem> Items, int Total, int Page, int PageSize);

public sealed record InspectionRepairQueuePage(IReadOnlyList<InspectionRepairQueueItem> Items, int Total, int Page, int PageSize);

public sealed record InspectionQueueResult(InspectionAssignmentStatus Status, InspectionQueuePage? Queue = null);

public sealed record InspectionRepairQueueResult(InspectionAssignmentStatus Status, InspectionRepairQueuePage? Queue = null);

public interface IInspectionQueueStore
{
    Task<InspectionQueueResult> InspectionsAsync(Guid actorId, InspectionQueueFilter filter,
        InspectionQueueStatus status, CancellationToken cancellationToken);

    Task<InspectionRepairQueueResult> RepairsAsync(Guid actorId, InspectionQueueFilter filter,
        InspectionRepairQueueStatus status, CancellationToken cancellationToken);
}

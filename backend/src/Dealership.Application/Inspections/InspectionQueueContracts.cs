using System.Globalization;
using Dealership.Domain.Common;

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
    Superseded
}

public sealed record InspectionQueueFilter(DateTimeOffset? From = null, DateTimeOffset? Until = null,
    Guid? BranchId = null, int Page = 1, int PageSize = 25, string Search = "")
{
    private static readonly TimeZoneInfo Malaysia = TimeZoneInfo.FindSystemTimeZoneById(DealershipLocale.MalaysiaTimeZoneId);

    public bool IsValid => Page >= 1 && PageSize is 10 or 25 or 50 && BranchId != Guid.Empty &&
        Search is not null && Search.Length <= 160 && !Search.Any(char.IsControl) &&
        (From is null && Until is null || From is not null && Until > From && Until - From <= TimeSpan.FromDays(366));

    public static InspectionQueueFilter? Parse(string? from, string? to, Guid? branchId,
        int page = 1, int pageSize = 25, string? search = null)
    {
        var filter = new InspectionQueueFilter(BranchId: branchId, Page: page, PageSize: pageSize, Search: search?.Trim() ?? "");
        if (from is null && to is null)
        {
            return filter.IsValid ? filter : null;
        }

        if (!DateOnly.TryParseExact(from, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var first) ||
            !DateOnly.TryParseExact(to, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var last) ||
            last < first || last.DayNumber - first.DayNumber >= 366 || first == DateOnly.MinValue || last == DateOnly.MaxValue)
        {
            return null;
        }

        filter = filter with
        {
            From = TimeZoneInfo.ConvertTimeToUtc(first.ToDateTime(TimeOnly.MinValue), Malaysia),
            Until = TimeZoneInfo.ConvertTimeToUtc(last.AddDays(1).ToDateTime(TimeOnly.MinValue), Malaysia)
        };
        return filter.IsValid ? filter : null;
    }
}

public sealed record InspectionQueueItem(Guid VehicleId, Guid BranchId, string BranchName, string RegistrationPlate,
    string Make, string Model, DateTimeOffset CheckedInAt, string VehicleStatus, Guid? InspectionId, Guid? TechnicianId,
    long? AssignmentVersion, long? Revision, InspectionQueueStatus Status);

public sealed record InspectionRepairQueueItem(Guid RequestId, Guid InspectionId, Guid VehicleId, Guid BranchId,
    string BranchName, string RegistrationPlate, Guid TechnicianId, long AssignmentVersion, long Revision,
    long RepairVersion, decimal Budget, DateTimeOffset RequestedAt, Guid? ApprovalId, Guid? CoveringApprovalId,
    InspectionRepairQueueStatus Status);

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

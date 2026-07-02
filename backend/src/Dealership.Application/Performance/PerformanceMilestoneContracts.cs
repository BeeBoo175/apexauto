using Dealership.Application.Accounts;
using Dealership.Application.Common;

namespace Dealership.Application.Performance;

public static class PerformanceMilestoneKinds
{
    public const string CheckIn = "CheckIn";
    public const string NoShowCorrection = "NoShowCorrection";
    public const string DriveCompleted = "DriveCompleted";
    public const string DriveNotCompleted = "DriveNotCompleted";
    public const string SaleSettled = "SaleSettled";
    public const string SaleDelivered = "SaleDelivered";
    public const string InspectionSigned = "InspectionSigned";
    public const string Reinspection = "Reinspection";
    public const string RepairCompleted = "RepairCompleted";
}

public sealed record PerformanceMilestoneQuery(PerformanceQuery Period, Guid PersonId, int Page, int PageSize)
{
    public static PerformanceMilestoneQuery? Parse(PerformanceQuery? period, Guid personId, int page, int pageSize)
    {
        return period is null || personId == Guid.Empty || !Pagination.IsValid(page, pageSize)
            ? null
            : new(period, personId, page, pageSize);
    }
}

// One completed piece of work attributed to a person at the branch recorded at the event. Reference is the vehicle
// registration, or the deal number for a sale. AgreedTotal is set for a delivered sale only, so payroll software can
// work out commission. HasAccess says whether the viewer may open the record today.
public sealed record PerformanceMilestone(string Kind, DateTimeOffset At, Guid BranchId, string BranchName,
    string Reference, string RecordKind, Guid RecordId, decimal? AgreedTotal, bool HasAccess);

public sealed record PerformanceMilestonePage(CurrentActor Actor, Guid PersonId, string PersonName,
    IReadOnlyList<PerformanceMilestone> Items, int Total, int Page, int PageSize);

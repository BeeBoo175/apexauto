using Dealership.Application.Accounts;
using Dealership.Application.Common;

namespace Dealership.Application.Performance;

public static class PerformancePersonStatuses
{
    public const string Active = "Active";
    // Still active, but now works at another branch than the one that recorded this row.
    public const string Transferred = "Transferred";
    public const string Deactivated = "Deactivated";
}

// A page of one person-per-branch row set. A person who worked at two branches appears once for each, because results
// belong to the branch recorded at the event.
public sealed record PerformanceTeamPage<T>(CurrentActor Actor, IReadOnlyList<T> Items, int Total, int Page,
    int PageSize, IReadOnlyList<PerformanceTeamPoint> Points);

// One person's volume of completed work and average days for the whole team in scope, whatever page is shown, so a
// scatter plot and a spread chart cover everyone. At most MaximumPoints people are returned, in the page order.
public sealed record PerformanceTeamPoint(Guid PersonId, Guid BranchId, string Name, int Volume, double? Days)
{
    public const int MaximumPoints = 500;
}

public sealed record PerformanceTeamQuery(PerformanceQuery Period, int Page, int PageSize)
{
    public static PerformanceTeamQuery? Parse(PerformanceQuery? period, int page, int pageSize)
    {
        return period is null || !Pagination.IsValid(page, pageSize) ? null : new(period, page, pageSize);
    }
}

// Sales & service. The Now counts are absent for a row whose branch is not the person's current branch.
// AssignmentToDeliveryDays averages the delivering agent's latest assignment up to the delivery.
public sealed record PerformanceSalesRow(Guid PersonId, string Name, string Role, Guid BranchId, string BranchName,
    Guid? PictureId, string Status, int CheckIns, int NoShowCorrections, int DrivesCompleted, int DrivesNotCompleted, int SalesSettled,
    int SalesDelivered, double? AssignmentToDeliveryDays, int? IntakeHandled, int? TestDrivesHeld, int? SalesHandled);

// Workshop. Re-inspections are counted and timed apart from first inspections. Intervals run from the latest
// assignment, or the reopening when that came later, to the sign-off.
public sealed record PerformanceWorkshopRow(Guid PersonId, string Name, string Role, Guid BranchId, string BranchName,
    Guid? PictureId, string Status, int InspectionsSigned, int Reinspections, int RepairsCompleted, double? AssignmentToSignOffDays,
    double? AssignmentToReinspectionDays, int? OpenDrafts, int? OpenRepairs);

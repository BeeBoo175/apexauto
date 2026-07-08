using Dealership.Application.Accounts;
using Dealership.Application.Common;

namespace Dealership.Application.Performance;

public enum PerformanceReadStatus
{
    Succeeded,
    Invalid,
    Forbidden,
    NotFound
}

public sealed record PerformanceReadResult<T>(PerformanceReadStatus Status, T? Value = default)
    where T : class;

// One dashboard request: a Malaysian-calendar period converted to a half-open UTC range and, for a GeneralManager,
// an optional branch. A BranchManager is always limited to their own branch whatever the request names.
public sealed record PerformanceQuery(DateTimeOffset From, DateTimeOffset Until, Guid? BranchId)
{
    public TimeSpan Length => Until - From;

    public static PerformanceQuery? Parse(string? from, string? to, Guid? branchId)
    {
        var range = ListDateRange.Parse(from, to);
        if (range is not { From: not null, Until: not null } || branchId == Guid.Empty)
        {
            return null;
        }

        return new(range.From.Value, range.Until.Value, branchId);
    }
}

public sealed record PerformancePeriodTotals(int VehiclesDelivered, decimal DeliveredValue, int ListedCount,
    double? CheckInToListedDays, int CustomerNoShows);

public sealed record PerformanceHeadline(CurrentActor Actor, PerformancePeriodTotals Current,
    PerformancePeriodTotals Previous);

public static class PerformancePipelineGroups
{
    public const string Unclaimed = "Unclaimed";
    public const string Handling = "Handling";
    public const string NeedsApproval = "NeedsApproval";
    public const string Approved = "Approved";
    public const string SettledUndelivered = "SettledUndelivered";

    public static readonly string[] Ordered = [Unclaimed, Handling, NeedsApproval, Approved, SettledUndelivered];
}

public sealed record PerformancePipelineGroup(string Group, int Count, decimal AgreedTotal);

public sealed record PerformancePipeline(CurrentActor Actor, IReadOnlyList<PerformancePipelineGroup> Groups);

// One Malaysian calendar week, named by its Monday, with the check-ins and deliveries completed in it and the
// agreed value of those deliveries.
public sealed record PerformanceStockWeek(DateOnly WeekStart, int CheckIns, int Deliveries, decimal DeliveredValue);

public sealed record PerformanceStockFlow(CurrentActor Actor, IReadOnlyList<PerformanceStockWeek> Weeks);

public static class PerformanceStages
{
    public const string AwaitingInspection = "AwaitingInspection";
    public const string InInspection = "InInspection";
    public const string InRepair = "InRepair";
    public const string AwaitingListing = "AwaitingListing";
    public const string Listed = "Listed";
    public const string SaleInProgress = "SaleInProgress";

    public static readonly string[] Ordered =
        [AwaitingInspection, InInspection, InRepair, AwaitingListing, Listed, SaleInProgress];
}

// InStageNow ignores the period. AverageDays is the elapsed calendar time of first passes that left the stage in
// the period, with the number of passes it averages; it is absent when no pass completed.
public sealed record PerformanceStage(string Stage, int InStageNow, int CompletedCount, double? AverageDays);

public sealed record PerformanceStageWaiting(CurrentActor Actor, IReadOnlyList<PerformanceStage> Stages);

// Open work now, split by how long the current holder has had it: under 2 days, 2 to 7 days and over 7 days.
public sealed record PerformanceWorkloadPerson(Guid Id, string Name, string Role, Guid BranchId, string BranchName,
    Guid? PictureId, int Under2Days, int TwoToSevenDays, int OverSevenDays)
{
    public int Total => Under2Days + TwoToSevenDays + OverSevenDays;
}

// One page of the people who hold open work, stretched people first. The counts describe the whole scope: Sales
// and Workshop count the people holding work in each team, Stretched counts those with work open over a week.
public sealed record PerformanceWorkload(CurrentActor Actor, IReadOnlyList<PerformanceWorkloadPerson> People,
    int Total, int SalesCount, int WorkshopCount, int StretchedCount, int Page, int PageSize);

// An empty team reads both teams. Search matches a person's name or branch.
public sealed record PerformanceWorkloadQuery(PerformanceQuery Period, string Team, string Search, int Page,
    int PageSize)
{
    public const string SalesTeam = "sales";
    public const string WorkshopTeam = "workshop";
    public const int MaximumSearchLength = 100;

    public static PerformanceWorkloadQuery? Parse(PerformanceQuery? period, string? team, string? search, int page,
        int pageSize)
    {
        var chosen = team?.Trim().ToLowerInvariant() ?? string.Empty;
        var term = search?.Trim() ?? string.Empty;
        if (period is null || !Pagination.IsValid(page, pageSize) || term.Length > MaximumSearchLength ||
            chosen is not ("" or SalesTeam or WorkshopTeam))
        {
            return null;
        }

        return new(period, chosen, term, page, pageSize);
    }
}

// Booked Intake appointments and test drives in the period by Malaysian weekday (Monday is 0) and clock hour.
public sealed record PerformanceRosterCell(int Weekday, int Hour, int Appointments, int TestDrives);

public sealed record PerformanceRoster(CurrentActor Actor, IReadOnlyList<PerformanceRosterCell> Cells);

// One row per active branch for a GeneralManager, with the headline measures of the period and the one before it.
// Weeks are this branch's own weekly flow over the period, so branches can be read as lines over time.
public sealed record PerformanceBranchRow(Guid BranchId, string BranchName, PerformancePeriodTotals Current,
    PerformancePeriodTotals Previous, IReadOnlyList<PerformanceStockWeek> Weeks);

public sealed record PerformanceBranchComparison(CurrentActor Actor, IReadOnlyList<PerformanceBranchRow> Branches);

public interface IPerformanceReads
{
    Task<PerformanceReadResult<PerformanceHeadline>> HeadlineAsync(Guid actorId, PerformanceQuery query,
        CancellationToken cancellationToken);

    Task<PerformanceReadResult<PerformancePipeline>> PipelineAsync(Guid actorId, PerformanceQuery query,
        CancellationToken cancellationToken);

    Task<PerformanceReadResult<PerformanceStockFlow>> StockFlowAsync(Guid actorId, PerformanceQuery query,
        CancellationToken cancellationToken);

    Task<PerformanceReadResult<PerformanceStageWaiting>> StageWaitingAsync(Guid actorId, PerformanceQuery query,
        CancellationToken cancellationToken);

    Task<PerformanceReadResult<PerformanceWorkload>> WorkloadAsync(Guid actorId, PerformanceWorkloadQuery query,
        CancellationToken cancellationToken);

    Task<PerformanceReadResult<PerformanceBranchComparison>> BranchComparisonAsync(Guid actorId,
        PerformanceQuery query, CancellationToken cancellationToken);

    Task<PerformanceReadResult<PerformanceMilestonePage>> MilestonesAsync(Guid actorId,
        PerformanceMilestoneQuery query, CancellationToken cancellationToken);

    Task<PerformanceReadResult<PerformanceRoster>> RosterAsync(Guid actorId, PerformanceQuery query,
        CancellationToken cancellationToken);

    Task<PerformanceReadResult<PerformanceTeamPage<PerformanceSalesRow>>> SalesTeamAsync(Guid actorId,
        PerformanceTeamQuery query, CancellationToken cancellationToken);

    Task<PerformanceReadResult<PerformanceTeamPage<PerformanceWorkshopRow>>> WorkshopTeamAsync(Guid actorId,
        PerformanceTeamQuery query, CancellationToken cancellationToken);
}

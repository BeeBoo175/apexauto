using Dealership.Application.Accounts;
using Dealership.Application.Common;

namespace Dealership.Application.Performance;

public enum PerformanceReadStatus
{
    Succeeded,
    Invalid,
    Forbidden
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

// One Malaysian calendar week, named by its Monday, with the check-ins and deliveries completed in it.
public sealed record PerformanceStockWeek(DateOnly WeekStart, int CheckIns, int Deliveries);

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
}

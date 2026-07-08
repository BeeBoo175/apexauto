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

// What the table asks for beside the period and page: search by name or branch, a name filter, a status filter,
// one sort and minimum counts per measure. The charts and headline figures always cover everyone; these shape only
// the table's rows.
public sealed record PerformanceTeamFilter(string Search, string Name, string Status, string Sort, bool Descending,
    IReadOnlyDictionary<string, int> Minimums)
{
    public const int MaximumTextLength = 100;
    public const int MaximumMinimums = 8;

    public static PerformanceTeamFilter None { get; } = new(string.Empty, string.Empty, string.Empty, string.Empty,
        false, new Dictionary<string, int>());

    // Minimums arrive as "measure:count" pairs separated by commas, for example "checkIns:5,salesDelivered:1".
    public static PerformanceTeamFilter? Parse(string? search, string? name, string? status, string? sort,
        string? direction, string? minimums)
    {
        var text = new[] { search, name }.Select(value => value?.Trim() ?? string.Empty).ToArray();
        var chosenStatus = status?.Trim() ?? string.Empty;
        var chosenSort = sort?.Trim() ?? string.Empty;
        var chosenDirection = direction?.Trim().ToLowerInvariant() ?? "asc";
        if (text.Any(value => value.Length > MaximumTextLength) || chosenSort.Length > 40 ||
            chosenDirection is not ("asc" or "desc") ||
            chosenStatus is not ("" or PerformancePersonStatuses.Active or PerformancePersonStatuses.Transferred or
                PerformancePersonStatuses.Deactivated))
        {
            return null;
        }

        var counts = new Dictionary<string, int>();
        foreach (var pair in (minimums ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries |
            StringSplitOptions.TrimEntries))
        {
            var parts = pair.Split(':');
            if (parts.Length != 2 || parts[0].Length is 0 or > 40 || !int.TryParse(parts[1], out var count) ||
                count < 0 || count > 1_000_000 || counts.Count >= MaximumMinimums)
            {
                return null;
            }

            counts[parts[0]] = count;
        }

        return new(text[0], text[1], chosenStatus, chosenSort, chosenDirection == "desc", counts);
    }
}

public sealed record PerformanceTeamQuery(PerformanceQuery Period, int Page, int PageSize,
    PerformanceTeamFilter Filter)
{
    public static PerformanceTeamQuery? Parse(PerformanceQuery? period, int page, int pageSize,
        PerformanceTeamFilter? filter = null)
    {
        return period is null || !Pagination.IsValid(page, pageSize) ? null :
            new(period, page, pageSize, filter ?? PerformanceTeamFilter.None);
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

using Dealership.Application.Performance;

namespace Dealership.Infrastructure.Performance;

// Team output is person-per-branch: a result belongs to the branch recorded at the event, so a transferred agent
// keeps their old results at the old branch and a deactivated one stays visible with their history.
// How a row answers the table's search, filters and sort. Measure keys are the column ids the page uses.
internal sealed record TeamColumns<T>(Func<T, Guid> Id, Func<T, string> Name, Func<T, string> Branch,
    Func<T, string> Status, IReadOnlyDictionary<string, Func<T, double?>> Measures);

internal static class PerformanceTeamTable
{
    // Search, sort and paging run in memory over one row per person and branch of a tab's roles, which staff
    // headcount bounds; move them into SQL if a branch ever grows to thousands of people.
    // The charts see everyone, in the default order. The table sees the rows the search, name, status and minimum
    // counts keep, in the chosen order, one page at a time; people stay in a fixed order within equal values.
    internal static PerformanceTeamPage<T> PageOf<T>(PerformanceScope scope, PerformanceTeamQuery team,
        IEnumerable<T> everyone, TeamColumns<T> columns, Func<T, PerformanceTeamPoint> point)
    {
        var all = DefaultOrder(everyone, columns).ToArray();
        var filter = team.Filter;
        var kept = all.Where(row => Keeps(row, filter, columns));
        var rows = Sorted(kept, filter, columns).ToArray();
        var items = rows.Skip((team.Page - 1) * team.PageSize).Take(team.PageSize).ToArray();
        var points = all.Take(PerformanceTeamPoint.MaximumPoints).Select(point).ToArray();
        return new(scope.Actor, items, rows.Length, team.Page, team.PageSize, points);
    }

    private static IOrderedEnumerable<T> DefaultOrder<T>(IEnumerable<T> rows, TeamColumns<T> columns)
    {
        return rows.OrderBy(columns.Branch, StringComparer.Ordinal).ThenBy(columns.Name, StringComparer.Ordinal)
            .ThenBy(columns.Id);
    }

    private static bool Keeps<T>(T row, PerformanceTeamFilter filter, TeamColumns<T> columns)
    {
        if (filter.Search.Length > 0 &&
            !columns.Name(row).Contains(filter.Search, StringComparison.OrdinalIgnoreCase) &&
            !columns.Branch(row).Contains(filter.Search, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (filter.Name.Length > 0 && !columns.Name(row).Contains(filter.Name, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (filter.Status.Length > 0 && columns.Status(row) != filter.Status)
        {
            return false;
        }

        foreach (var (key, minimum) in filter.Minimums)
        {
            if (columns.Measures.TryGetValue(key, out var measure) && (measure(row) ?? 0) < minimum)
            {
                return false;
            }
        }

        return true;
    }

    private static IEnumerable<T> Sorted<T>(IEnumerable<T> rows, PerformanceTeamFilter filter, TeamColumns<T> columns)
    {
        if (filter.Sort.Length == 0)
        {
            return DefaultOrder(rows, columns);
        }

        IOrderedEnumerable<T> ordered;
        if (columns.Measures.TryGetValue(filter.Sort, out var measure))
        {
            // People with no figure for a measure come last whichever way it is sorted.
            var withFigure = rows.OrderBy(row => measure(row) is null);
            ordered = filter.Descending
                ? withFigure.ThenByDescending(row => measure(row))
                : withFigure.ThenBy(row => measure(row));
        }
        else
        {
            Func<T, string> text = filter.Sort switch
            {
                "branch" => columns.Branch,
                "status" => columns.Status,
                _ => columns.Name
            };
            ordered = filter.Descending
                ? rows.OrderByDescending(text, StringComparer.Ordinal)
                : rows.OrderBy(text, StringComparer.Ordinal);
        }

        return ordered.ThenBy(columns.Branch, StringComparer.Ordinal).ThenBy(columns.Name, StringComparer.Ordinal)
            .ThenBy(columns.Id);
    }

    internal static double? AverageDays(IEnumerable<double> seconds)
    {
        var values = seconds.Where(value => value >= 0).ToArray();
        return values.Length == 0 ? null : values.Average() / TimeSpan.SecondsPerDay;
    }
}

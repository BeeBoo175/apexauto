using Dealership.Application.Performance;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Performance;

public sealed partial class PerformanceReads
{
    // Team output is person-per-branch: a result belongs to the branch recorded at the event, so a transferred agent
    // keeps their old results at the old branch and a deactivated one stays visible with their history.
    // How a row answers the table's search, filters and sort. Measure keys are the column ids the page uses.
    private sealed record TeamColumns<T>(Func<T, Guid> Id, Func<T, string> Name, Func<T, string> Branch,
        Func<T, string> Status, IReadOnlyDictionary<string, Func<T, double?>> Measures);

    // The charts see everyone, in the default order. The table sees the rows the search, name, status and minimum
    // counts keep, in the chosen order, one page at a time; people stay in a fixed order within equal values.
    private static PerformanceTeamPage<T> PageOf<T>(PerformanceScope scope, PerformanceTeamQuery team,
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

    private static double? AverageDays(IEnumerable<double> seconds)
    {
        var values = seconds.Where(value => value >= 0).ToArray();
        return values.Length == 0 ? null : values.Average() / TimeSpan.SecondsPerDay;
    }

    // The staff of one tab's roles, with what is needed to tag a row Active, Transferred or Deactivated.
    private sealed class TeamPeople
    {
        private readonly Dictionary<Guid, Person> people;
        private readonly Dictionary<Guid, string> branches;

        private TeamPeople(Dictionary<Guid, Person> people, Dictionary<Guid, string> branches)
        {
            this.people = people;
            this.branches = branches;
        }

        public static async Task<TeamPeople> ReadAsync(DealershipDbContext database, string[] roles,
            CancellationToken cancellationToken)
        {
            var rows = await (from user in database.Users.AsNoTracking()
                              join profile in database.StaffProfiles.AsNoTracking() on user.Id equals profile.UserId
                              where roles.Contains(user.Role)
                              select new
                              {
                                  user.Id,
                                  profile.Name,
                                  user.Role,
                                  user.IsActive,
                                  user.BranchId,
                                  PictureId = database.AccountPictures
                                      .Where(picture => picture.UserId == user.Id && picture.RemovedAt == null)
                                      .Select(picture => (Guid?)picture.Id).FirstOrDefault()
                              })
                .ToListAsync(cancellationToken);
            var branches = await database.Branches.AsNoTracking().ToDictionaryAsync(branch => branch.Id,
                branch => branch.Name, cancellationToken);
            return new(rows.ToDictionary(row => row.Id,
                row => new Person(row.Name, row.Role, row.IsActive, row.BranchId, row.PictureId)), branches);
        }

        public bool Includes(Guid personId) => people.ContainsKey(personId);

        public string Name(Guid personId) => people[personId].Name;

        public string Role(Guid personId) => people[personId].Role;

        public Guid? Picture(Guid personId) => people[personId].PictureId;

        public string BranchName(Guid branchId) => branches.GetValueOrDefault(branchId, string.Empty);

        // Active staff of a role in scope, each at their current branch, so people with no results still show zeros.
        public IEnumerable<(Guid PersonId, Guid BranchId)> Current(string role, Guid? branchId)
        {
            return people.Where(item => item.Value.IsActive && item.Value.Role == role &&
                    item.Value.BranchId is { } own && (branchId == null || own == branchId))
                .Select(item => (item.Key, item.Value.BranchId!.Value));
        }

        public bool IsCurrent(Guid personId, Guid branchId)
        {
            var person = people[personId];
            return person.IsActive && person.BranchId == branchId;
        }

        public string Status(Guid personId, Guid branchId)
        {
            if (!people[personId].IsActive)
            {
                return PerformancePersonStatuses.Deactivated;
            }

            return IsCurrent(personId, branchId)
                ? PerformancePersonStatuses.Active
                : PerformancePersonStatuses.Transferred;
        }

        private sealed record Person(string Name, string Role, bool IsActive, Guid? BranchId, Guid? PictureId);
    }
}

using Dealership.Application.Accounts;
using Dealership.Domain.Accounts;
using Dealership.Application.Performance;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Performance;

// The staff of one tab's roles, with what is needed to tag a row Active, Transferred or Deactivated.
internal sealed class TeamPeople
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
                              Name = profile.Name ?? PersonLabels.FormerEmployee,
                              user.Role,
                              user.IsActive,
                              user.BranchId,
                              PictureId = database.AccountPictures
                                  .Where(AccountPicture.Current).Where(picture => picture.UserId == user.Id)
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

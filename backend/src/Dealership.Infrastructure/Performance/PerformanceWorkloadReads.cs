using System.Data;
using Dealership.Application.Accounts;
using Dealership.Application.Performance;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Performance;

public sealed class PerformanceWorkloadReads(DealershipDbContext database) : IPerformanceWorkloadReads
{
    private static readonly string[] WorkloadRoles =
        [AccountRoles.SalesAgent, AccountRoles.BranchManager, AccountRoles.WorkshopTechnician];

    // Open work now, held by people who are currently in scope. Age runs from the current holder's assignment, so a
    // reassigned item starts again for its new holder.
    public async Task<PerformanceReadResult<PerformanceWorkload>> WorkloadAsync(Guid actorId,
        PerformanceWorkloadQuery query, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead,
            cancellationToken);
        var scope = await PerformanceScopes.ResolveAsync(database, actorId, query.Period, cancellationToken);
        if (scope is null)
        {
            return new(PerformanceReadStatus.Forbidden);
        }

        var now = await database.DatabaseNowAsync(cancellationToken);
        var branchId = scope.BranchId;
        var staff = PersonRows.Staff(database);
        var people = await (from user in database.Users.AsNoTracking()
                            join branch in database.Branches.AsNoTracking() on user.BranchId equals branch.Id
                            join profile in database.StaffProfiles.AsNoTracking() on user.Id equals profile.UserId
                            where user.IsActive && WorkloadRoles.Contains(user.Role) &&
                                (branchId == null || user.BranchId == branchId)
                            select new
                            {
                                user.Id,
                                Name = profile.Name ?? PersonLabels.FormerEmployee,
                                user.Role,
                                BranchId = branch.Id,
                                BranchName = branch.Name,
                                PictureId = staff.Where(person => person.UserId == user.Id)
                                    .Select(person => person.PictureId).FirstOrDefault()
                            })
            .ToListAsync(cancellationToken);

        var started = new List<(Guid PersonId, DateTimeOffset Since)>();
        started.AddRange(await PerformanceWork.IntakeWorkAsync(database, branchId, now, cancellationToken));
        started.AddRange(await PerformanceWork.TestDriveWorkAsync(database, branchId, cancellationToken));
        started.AddRange(await PerformanceWork.SaleWorkAsync(database, branchId, cancellationToken));
        started.AddRange(await PerformanceWork.DraftWorkAsync(database, branchId, cancellationToken));
        started.AddRange(await PerformanceWork.RepairWorkAsync(database, branchId, cancellationToken));
        await transaction.CommitAsync(cancellationToken);

        var holders = people.Select(person =>
            {
                var ages = started.Where(item => item.PersonId == person.Id).Select(item => now - item.Since).ToArray();
                return new PerformanceWorkloadPerson(person.Id, person.Name, person.Role, person.BranchId,
                    person.BranchName, person.PictureId, ages.Count(age => age < TimeSpan.FromDays(2)),
                    ages.Count(age => age >= TimeSpan.FromDays(2) && age <= TimeSpan.FromDays(7)),
                    ages.Count(age => age > TimeSpan.FromDays(7)));
            }).Where(person => person.Total > 0).ToArray();

        // Stretched people come first as a state, never a ranking; a reader of every branch sees branches in order.
        // Within each group people stay in alphabetical order, so a page boundary never depends on a measure.
        var matching = holders.Where(person => InTeam(person, query.Team) && Matches(person, query.Search))
            .OrderBy(person => person.OverSevenDays > 0 ? 0 : 1)
            .ThenBy(person => branchId == null ? person.BranchName : string.Empty, StringComparer.Ordinal)
            .ThenBy(person => person.Name, StringComparer.Ordinal).ThenBy(person => person.Id).ToArray();
        var page = matching.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToArray();
        return new(PerformanceReadStatus.Succeeded, new PerformanceWorkload(scope.Actor, page, matching.Length,
            holders.Count(person => InTeam(person, PerformanceWorkloadQuery.SalesTeam)),
            holders.Count(person => InTeam(person, PerformanceWorkloadQuery.WorkshopTeam)),
            holders.Count(person => person.OverSevenDays > 0), query.Page, query.PageSize));
    }

    private static bool InTeam(PerformanceWorkloadPerson person, string team)
    {
        var workshop = person.Role == AccountRoles.WorkshopTechnician;
        return team switch
        {
            PerformanceWorkloadQuery.SalesTeam => !workshop,
            PerformanceWorkloadQuery.WorkshopTeam => workshop,
            _ => true
        };
    }

    private static bool Matches(PerformanceWorkloadPerson person, string search)
    {
        return search.Length == 0 || person.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
            person.BranchName.Contains(search, StringComparison.OrdinalIgnoreCase);
    }
}

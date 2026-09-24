using Dealership.Application.Accounts;
using Dealership.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Persistence;

internal static class StaffHandlerRows
{
    public static IQueryable<AccountUser> Eligible(DealershipDbContext database)
    {
        return database.Users.AsNoTracking().Where(user => user.IsActive &&
            (user.Role == AccountRoles.SalesAgent || user.Role == AccountRoles.BranchManager) &&
            database.Branches.Any(branch => branch.Id == user.BranchId && branch.IsActive));
    }

    // Display identity of one staff account for single-record reads; list reads project it inline.
    public static async Task<(string? Name, Guid? PictureId)> DisplayAsync(DealershipDbContext database,
        Guid? userId, CancellationToken cancellationToken)
    {
        if (userId is not { } id)
        {
            return (null, null);
        }

        var person = await PersonRows.Staff(database).Where(item => item.UserId == id)
            .FirstOrDefaultAsync(cancellationToken);
        return (person?.Name, person?.PictureId);
    }

    public static Task<StaffHandler[]> ReadAsync(DealershipDbContext database, Guid? branchId,
        CancellationToken cancellationToken)
    {
        var staff = PersonRows.Staff(database);
        return Eligible(database).Where(user => user.BranchId == branchId)
            .OrderBy(user => user.Email).ThenBy(user => user.Id)
            .Select(user => new StaffHandler(user.Id, user.Email ?? string.Empty,
                staff.Where(person => person.UserId == user.Id).Select(person => person.Name).FirstOrDefault()!,
                staff.Where(person => person.UserId == user.Id).Select(person => person.PictureId).FirstOrDefault()))
            .ToArrayAsync(cancellationToken);
    }
}

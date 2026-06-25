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

        var name = await database.StaffProfiles.AsNoTracking().Where(profile => profile.UserId == id)
            .Select(profile => profile.Name).FirstOrDefaultAsync(cancellationToken);
        var pictureId = await database.AccountPictures.AsNoTracking()
            .Where(picture => picture.UserId == id && picture.RemovedAt == null)
            .Select(picture => (Guid?)picture.Id).FirstOrDefaultAsync(cancellationToken);
        return (name, pictureId);
    }

    public static Task<StaffHandler[]> ReadAsync(DealershipDbContext database, Guid? branchId,
        CancellationToken cancellationToken)
    {
        return Eligible(database).Where(user => user.BranchId == branchId)
            .OrderBy(user => user.Email).ThenBy(user => user.Id)
            .Select(user => new StaffHandler(user.Id, user.Email ?? string.Empty,
                database.StaffProfiles.Where(profile => profile.UserId == user.Id)
                    .Select(profile => profile.Name).FirstOrDefault() ?? string.Empty,
                database.AccountPictures.Where(picture => picture.UserId == user.Id && picture.RemovedAt == null)
                    .Select(picture => (Guid?)picture.Id).FirstOrDefault()))
            .ToArrayAsync(cancellationToken);
    }
}

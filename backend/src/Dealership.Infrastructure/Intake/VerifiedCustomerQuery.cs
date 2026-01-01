using Dealership.Application.Accounts;
using Dealership.Infrastructure.Identity;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Intake;

internal static class VerifiedCustomerQuery
{
    public static IQueryable<AccountUser> ForOwner(DealershipDbContext database, Guid ownerId, bool lockUser)
    {
        var users = database.Users.AsQueryable();

        if (lockUser)
        {
            users = database.Users.FromSqlInterpolated(
                $"SELECT * FROM \"AspNetUsers\" WHERE \"Id\" = {ownerId} FOR UPDATE");
        }

        return users.Where(user => user.Id == ownerId && user.IsActive && user.Role == AccountRoles.Customer &&
            user.EmailConfirmed && database.CustomerProfiles.Any(profile => profile.UserId == user.Id));
    }
}

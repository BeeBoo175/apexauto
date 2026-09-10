using Dealership.Domain.Accounts;
using Dealership.Application.Accounts;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Accounts;

public sealed class AccountProfileQuery(DealershipDbContext database) : IAccountProfileQuery
{
    public async Task<AccountProfile?> GetProfileAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await (from user in database.Users
                      where user.Id == userId && user.IsActive
                      join customer in database.CustomerProfiles on user.Id equals customer.UserId into customers
                      from customer in customers.DefaultIfEmpty()
                      join staff in database.StaffProfiles on user.Id equals staff.UserId into staffProfiles
                      from staff in staffProfiles.DefaultIfEmpty()
                      join branch in database.Branches on user.BranchId equals branch.Id into branches
                      from branch in branches.DefaultIfEmpty()
                      join picture in database.AccountPictures.Where(AccountPicture.Current) on user.Id equals picture.UserId into pictures
                      from picture in pictures.DefaultIfEmpty()
                      select new AccountProfile(user.Id, user.Role, customer.LegalName ?? staff.Name,
                          user.AccountUsername, user.Email!, user.EmailConfirmed, user.BranchId, branch.Name,
                          staff.Mobile, (Guid?)picture.Id))
            .SingleOrDefaultAsync(cancellationToken);
    }
}

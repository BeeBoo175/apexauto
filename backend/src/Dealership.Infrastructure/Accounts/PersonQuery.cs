using Dealership.Application.Accounts;
using Dealership.Domain.Accounts;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Accounts;

public sealed class PersonQuery(DealershipDbContext database) : IPersonQuery
{
    public async Task<Person?> FindAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await (from user in database.Users
                      where user.Id == userId
                      join customer in database.CustomerProfiles on user.Id equals customer.UserId into customers
                      from customer in customers.DefaultIfEmpty()
                      join staff in database.StaffProfiles on user.Id equals staff.UserId into staffProfiles
                      from staff in staffProfiles.DefaultIfEmpty()
                      join branch in database.Branches on user.BranchId equals branch.Id into branches
                      from branch in branches.DefaultIfEmpty()
                      join picture in database.AccountPictures.Where(AccountPicture.Current)
                          on user.Id equals picture.UserId into pictures
                      from picture in pictures.DefaultIfEmpty()
                      // An erased person keeps the label of their role, as AccountNames reads it.
                      let erasedLabel = user.Role == AccountRoles.Customer
                          ? PersonLabels.FormerCustomer
                          : PersonLabels.FormerEmployee
                      select new Person(user.Id, user.Role, customer.LegalName ?? staff.Name ?? erasedLabel,
                          user.AccountUsername ?? string.Empty, user.IsActive, user.BranchId, branch.Name, user.Email,
                          staff.Mobile, (Guid?)picture.Id))
            .SingleOrDefaultAsync(cancellationToken);
    }
}

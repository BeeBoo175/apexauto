using Dealership.Application.Accounts;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Accounts;

public sealed class CustomerAccountQuery(DealershipDbContext database) : ICustomerAccountQuery
{
    public async Task<CustomerAccount?> GetAccountAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await (from profile in database.CustomerProfiles
                      join user in database.Users on profile.UserId equals user.Id
                      where profile.UserId == userId && user.Role == AccountRoles.Customer && user.IsActive
                      select new CustomerAccount(user.Id, profile.LegalName, user.Email!, user.EmailConfirmed))
            .SingleOrDefaultAsync(cancellationToken);
    }
}

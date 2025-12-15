using Dealership.Application.Accounts;
using Microsoft.AspNetCore.Identity;

namespace Dealership.Infrastructure.Identity;

public sealed class AccountUser : IdentityUser<Guid>
{
    public string Role { get; set; } = AccountRoles.Customer;

    public Guid? BranchId { get; set; }

    public bool IsActive { get; set; } = true;
}

using Dealership.Application.Accounts;
using Microsoft.AspNetCore.Identity;

namespace Dealership.Infrastructure.Identity;

public sealed class AccountUser : IdentityUser<Guid>
{
    public string Role { get; set; } = AccountRoles.Customer;

    // A unique display handle; login and recovery stay on email.
    public string AccountUsername { get; set; } = string.Empty;

    public Guid? BranchId { get; set; }

    public bool IsActive { get; set; } = true;

    // Set only while an Administrator has suspended the account; a suspended account is also inactive, so every
    // eligibility check that already requires IsActive excludes it, and deactivation leaves this empty.
    public DateTimeOffset? SuspendedAt { get; set; }
}

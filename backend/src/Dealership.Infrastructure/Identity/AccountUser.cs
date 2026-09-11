using Dealership.Application.Accounts;
using Microsoft.AspNetCore.Identity;

namespace Dealership.Infrastructure.Identity;

public sealed class AccountUser : IdentityUser<Guid>
{
    public string Role { get; set; } = AccountRoles.Customer;

    // A unique display handle; login and recovery stay on email.
    // Null only once the account is erased, which releases the handle for a new account.
    public string? AccountUsername { get; set; }

    public Guid? BranchId { get; set; }

    public bool IsActive { get; set; } = true;

    // Set only while an Administrator has suspended the account; a suspended account is also inactive, so every
    // eligibility check that already requires IsActive excludes it, and deactivation leaves this empty.
    public DateTimeOffset? SuspendedAt { get; set; }

    // Irreversible end of access and erasure in one state: the personal columns are NULL, the account is inactive and
    // has no password, and the row stays as the minimal identity that history and replay keys refer to.
    public DateTimeOffset? ClosedAt { get; set; }
}

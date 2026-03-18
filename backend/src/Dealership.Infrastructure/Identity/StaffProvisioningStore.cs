using Dealership.Application.Accounts;
using Dealership.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Identity;

public sealed class StaffProvisioningStore(DealershipDbContext database, UserManager<AccountUser> users,
    IPasswordHasher<AccountUser> passwords)
    : IStaffProvisioningStore
{
    public async Task<ProvisioningResult> ProvisionStaffAsync(ValidatedStaffProvisioning staff,
        CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        await LockFixtureAsync(staff.Id, cancellationToken);
        var existing = await database.Users.AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == staff.Id, cancellationToken);

        if (existing is not null)
        {
            var matches = Matches(existing, staff, passwords);
            return new ProvisioningResult(false, matches);
        }

        if (staff.BranchId.HasValue && !await database.Branches.AnyAsync(
            branch => branch.Id == staff.BranchId && branch.IsActive, cancellationToken))
        {
            throw new InvalidOperationException("Staff provisioning requires an active assigned branch.");
        }

        var user = new AccountUser
        {
            Id = staff.Id,
            UserName = staff.Email,
            Email = staff.Email,
            EmailConfirmed = true,
            Role = staff.Role,
            BranchId = staff.BranchId
        };
        var result = await users.CreateAsync(user, staff.Password);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException("Staff provisioning failed: " +
                string.Join(", ", result.Errors.Select(error => error.Code)));
        }

        await transaction.CommitAsync(cancellationToken);
        return new ProvisioningResult(true, true);
    }

    internal static bool Matches(AccountUser existing, ValidatedStaffProvisioning requested,
        IPasswordHasher<AccountUser> passwords)
    {
        return existing.Email == requested.Email && existing.Role == requested.Role &&
            existing.BranchId == requested.BranchId && existing.IsActive && existing.EmailConfirmed &&
            existing.PasswordHash is not null &&
            passwords.VerifyHashedPassword(existing, existing.PasswordHash, requested.Password) != PasswordVerificationResult.Failed;
    }

    private Task<int> LockFixtureAsync(Guid id, CancellationToken cancellationToken)
    {
        // Transaction locks serialize fixture creation across processes, including an absent row.
        return database.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({id.ToString()}, 0))", cancellationToken);
    }
}

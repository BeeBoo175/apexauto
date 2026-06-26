using Dealership.Application.Accounts;
using Dealership.Domain.Staff;
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
            var profile = await database.StaffProfiles.AsNoTracking()
                .SingleOrDefaultAsync(candidate => candidate.UserId == staff.Id, cancellationToken);
            var matches = Matches(existing, profile, staff, passwords);
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
            AccountUsername = staff.Username ?? await FindFreeUsernameAsync(staff.Email, cancellationToken),
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

        // Identity and profile writes share the scoped DbContext, so neither can commit alone.
        database.StaffProfiles.Add(new StaffProfile(user.Id, staff.Name, staff.Mobile));
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new ProvisioningResult(true, true);
    }

    internal static bool Matches(AccountUser existing, StaffProfile? profile, ValidatedStaffProvisioning requested,
        IPasswordHasher<AccountUser> passwords)
    {
        return profile is not null && (requested.Username is null || existing.AccountUsername == requested.Username) &&
            profile.Name == requested.Name && profile.Mobile == requested.Mobile &&
            existing.Email == requested.Email && existing.Role == requested.Role &&
            existing.BranchId == requested.BranchId && existing.IsActive && existing.EmailConfirmed &&
            existing.PasswordHash is not null &&
            passwords.VerifyHashedPassword(existing, existing.PasswordHash, requested.Password) != PasswordVerificationResult.Failed;
    }

    private async Task<string> FindFreeUsernameAsync(string email, CancellationToken cancellationToken)
    {
        var candidate = UsernamePolicy.DeriveFromEmail(email);
        var suffix = 1;

        // The stored uppercase key is the uniqueness key, so a clash is judged exactly as the index judges it.
        while (await IsTakenAsync(candidate, cancellationToken))
        {
            suffix++;
            candidate = UsernamePolicy.WithSuffix(UsernamePolicy.DeriveFromEmail(email), suffix);
        }

        return candidate;
    }

    private Task<bool> IsTakenAsync(string username, CancellationToken cancellationToken)
    {
        var key = username.ToUpperInvariant();
        return database.Users.AnyAsync(user => EF.Property<string>(user, "NormalizedAccountUsername") == key,
            cancellationToken);
    }

    private Task<int> LockFixtureAsync(Guid id, CancellationToken cancellationToken)
    {
        // Transaction locks serialize fixture creation across processes, including an absent row.
        return database.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({id.ToString()}, 0))", cancellationToken);
    }
}

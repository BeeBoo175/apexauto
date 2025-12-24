using Dealership.Application.Accounts;
using Dealership.Infrastructure.Identity;
using Dealership.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Dealership.Infrastructure.Development;

public sealed record DevelopmentFixtureReport(string Fixture, string State, bool RequiresAttention);

public sealed class DevelopmentFixtureRunner(DealershipDbContext database, RegistrationService registration,
    StaffProvisioningService staff, IPasswordHasher<AccountUser> passwords)
{
    public async Task<IReadOnlyList<DevelopmentFixtureReport>> SeedAsync(DevelopmentFixtureTarget target,
        CancellationToken cancellationToken)
    {
        EnsureTargetMatches(target);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(2));
        var token = deadline.Token;
        await using var lockConnection = await DevelopmentFixtureLock.AcquireAsync(target, token);

        // A dedicated nonpooled connection owns the run lock; disposal releases it after failure or restart.
        var reports = new List<DevelopmentFixtureReport>();
        var branch = await staff.ProvisionBranchAsync(DevelopmentFixtureManifest.Branch, token);
        reports.Add(Report("Glenmarie branch", branch));
        reports.Add(await SeedCustomerAsync(token));

        foreach (var account in DevelopmentFixtureManifest.Staff)
        {
            var provisioned = await staff.ProvisionStaffAsync(account, token);
            reports.Add(Report(account.Role, provisioned));
        }

        return reports;
    }

    public async Task RebuildAsync(DevelopmentFixtureTarget target, string? confirmation,
        CancellationToken cancellationToken)
    {
        EnsureTargetMatches(target);
        var databaseName = new NpgsqlConnectionStringBuilder(target.ConnectionString).Database;

        if (confirmation != $"rebuild {databaseName}")
        {
            throw new InvalidOperationException("Database rebuild requires the exact typed target confirmation.");
        }

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(2));
        await using var lockConnection = await DevelopmentFixtureLock.AcquireAsync(target, deadline.Token);
        await using var permissions = new NpgsqlCommand(
            "SELECT rolsuper OR rolcreatedb FROM pg_roles WHERE rolname = current_user", lockConnection);

        if (await permissions.ExecuteScalarAsync(deadline.Token) is not true)
        {
            throw new InvalidOperationException("Database rebuild requires maintenance credentials able to recreate the database.");
        }

        // The lock lives in postgres so it survives removal and recreation of the target database.
        await database.Database.EnsureDeletedAsync(deadline.Token);
        await database.Database.MigrateAsync(deadline.Token);
        database.ChangeTracker.Clear();
    }

    public async Task<IReadOnlyList<DevelopmentFixtureReport>> ReviewAsync(DevelopmentFixtureTarget target,
        CancellationToken cancellationToken)
    {
        EnsureTargetMatches(target);
        var reports = new List<DevelopmentFixtureReport>();
        var branch = await database.Branches.AsNoTracking().SingleOrDefaultAsync(
            candidate => candidate.Id == DevelopmentFixtureManifest.Branch.Id, cancellationToken);
        reports.Add(new DevelopmentFixtureReport("Glenmarie branch", branch is null ? "missing" :
            $"{branch.Name}; {branch.City}; {branch.State}; active={branch.IsActive}", branch is null));
        var customer = await database.CustomerProfiles.AsNoTracking().SingleOrDefaultAsync(
            profile => profile.RegistrationKey == DevelopmentFixtureManifest.Customer.RegistrationKey, cancellationToken);
        reports.Add(await ReviewAccountAsync("Customer", customer?.UserId, cancellationToken));

        foreach (var account in DevelopmentFixtureManifest.Staff)
        {
            reports.Add(await ReviewAccountAsync(account.Role, account.Id, cancellationToken));
        }

        return reports;
    }

    private async Task<DevelopmentFixtureReport> SeedCustomerAsync(CancellationToken cancellationToken)
    {
        var requested = DevelopmentFixtureManifest.Customer;
        var profile = await database.CustomerProfiles.AsNoTracking().SingleOrDefaultAsync(
            candidate => candidate.RegistrationKey == requested.RegistrationKey, cancellationToken);

        if (profile is null)
        {
            var created = await registration.RegisterAsync(requested, cancellationToken);
            var state = created.Status == RegistrationStatus.Succeeded ? "created" : "conflict; preserved";
            return new DevelopmentFixtureReport("Customer", state, created.Status != RegistrationStatus.Succeeded);
        }

        var user = await database.Users.AsNoTracking().SingleAsync(
            candidate => candidate.Id == profile.UserId, cancellationToken);
        var matches = user.Email == requested.Email && user.Role == AccountRoles.Customer &&
            user.BranchId is null && user.IsActive && profile.LegalName == requested.LegalName &&
            profile.Nric == requested.Nric && profile.Mobile == requested.Mobile &&
            profile.ResidentialAddress == requested.ResidentialAddress && user.PasswordHash is not null &&
            passwords.VerifyHashedPassword(user, user.PasswordHash, requested.Password!) != PasswordVerificationResult.Failed;
        return Report("Customer", new ProvisioningResult(false, matches));
    }

    private async Task<DevelopmentFixtureReport> ReviewAccountAsync(string fixture, Guid? id,
        CancellationToken cancellationToken)
    {
        var account = await database.Users.AsNoTracking().SingleOrDefaultAsync(
            candidate => candidate.Id == id, cancellationToken);
        var state = account is null ? "missing" :
            $"role={account.Role}; branch={account.BranchId}; active={account.IsActive}; verified={account.EmailConfirmed}";
        return new DevelopmentFixtureReport(fixture, state, account is null);
    }

    private void EnsureTargetMatches(DevelopmentFixtureTarget target)
    {
        var actual = new NpgsqlConnectionStringBuilder(database.Database.GetConnectionString());
        var expected = new NpgsqlConnectionStringBuilder(target.ConnectionString);

        if (actual.Host != expected.Host || actual.Port != expected.Port || actual.Database != expected.Database ||
            actual.Username != expected.Username || actual.Password != expected.Password)
        {
            throw new InvalidOperationException("Fixture target must match the service database connection.");
        }
    }

    private static DevelopmentFixtureReport Report(string fixture, ProvisioningResult result)
    {
        if (result.Created)
        {
            return new DevelopmentFixtureReport(fixture, "created", false);
        }

        return new DevelopmentFixtureReport(fixture, result.MatchesRequestedState ? "matches" : "drift; preserved",
            !result.MatchesRequestedState);
    }
}

using Dealership.Application.Accounts;
using Dealership.Application.Branches;
using Dealership.Infrastructure.Branches;
using Dealership.Infrastructure.Identity;
using Dealership.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Development;

public sealed class DevelopmentAccountFixtures(DealershipDbContext database, DevelopmentCustomerFixtures customers,
    StaffProvisioningService staff, BranchProvisioningService branches, IPasswordHasher<AccountUser> passwords)
{
    public async Task<IReadOnlyList<DevelopmentFixtureReport>> SeedAsync(DevelopmentFixtureTarget target,
        CancellationToken cancellationToken)
    {
        target.EnsureMatches(database.Database.GetConnectionString());
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(2));
        await using var runLock = await DevelopmentFixtureLock.AcquireAsync(target, deadline.Token);
        return await RunAsync(true, deadline.Token);
    }

    public Task<IReadOnlyList<DevelopmentFixtureReport>> ReviewAsync(DevelopmentFixtureTarget target,
        CancellationToken cancellationToken)
    {
        target.EnsureMatches(database.Database.GetConnectionString());
        return RunAsync(false, cancellationToken);
    }

    // The full snapshot already holds the shared run lock; both modes provision the same account baseline.
    public async Task<IReadOnlyList<DevelopmentFixtureReport>> RunAsync(bool seed, CancellationToken cancellationToken)
    {
        var reports = new List<DevelopmentFixtureReport>();
        foreach (var branch in DevelopmentFixtureManifest.Branches)
        {
            if (seed)
            {
                var provisioned = await branches.ProvisionBranchAsync(branch, cancellationToken);
                reports.Add(Report(branch.Name, provisioned.Created, provisioned.MatchesRequestedState));
                continue;
            }

            var existing = await database.Branches.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == branch.Id, cancellationToken);
            var matches = existing is not null && BranchProvisioningStore.Matches(existing, branch);
            var state = existing is null ? "missing" : "drift; preserved";
            reports.Add(new(branch.Name, matches ? "matches" : state, !matches));
        }

        foreach (var customer in DevelopmentFixtureManifest.Customers)
        {
            reports.Add(seed
                ? await customers.SeedAsync(customer, cancellationToken)
                : await customers.ReviewAsync(customer, cancellationToken));
        }

        foreach (var account in DevelopmentFixtureManifest.Staff)
        {
            if (seed)
            {
                var provisioned = await staff.ProvisionStaffAsync(account, cancellationToken);
                reports.Add(Report($"{account.Role} ({account.Email})", provisioned.Created, provisioned.MatchesRequestedState));
                continue;
            }

            reports.Add(await ReviewStaffAsync(account, cancellationToken));
        }

        return reports;
    }

    private async Task<DevelopmentFixtureReport> ReviewStaffAsync(StaffProvisioningRequest requested,
        CancellationToken cancellationToken)
    {
        var account = await database.Users.AsNoTracking().SingleOrDefaultAsync(
            candidate => candidate.Id == requested.Id, cancellationToken);
        var profile = await database.StaffProfiles.AsNoTracking().SingleOrDefaultAsync(
            candidate => candidate.UserId == requested.Id, cancellationToken);
        var staff = new ValidatedStaffProvisioning(requested.Id, requested.Email, requested.Password,
            requested.Role, requested.BranchId, requested.Name, requested.Mobile);
        var matches = account is not null && StaffProvisioningStore.Matches(account, profile, staff, passwords);
        var state = account is null ? "missing" : "drift; preserved";
        return new($"{requested.Role} ({requested.Email})", matches ? "matches" : state, !matches);
    }

    private static DevelopmentFixtureReport Report(string fixture, bool created, bool matches)
    {
        if (created)
        {
            return new(fixture, "created", false);
        }

        return new(fixture, matches ? "matches" : "drift; preserved", !matches);
    }
}

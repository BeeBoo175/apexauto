using Dealership.Application.Accounts;
using Dealership.Application.Branches;
using Dealership.Infrastructure.Branches;
using Dealership.Infrastructure.Identity;
using Dealership.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Development;

public sealed record DevelopmentFixtureReport(string Fixture, string State, bool RequiresAttention);

public sealed class DevelopmentFixtureRunner(DealershipDbContext database, DevelopmentCustomerFixtures customers,
    StaffProvisioningService staff, BranchProvisioningService branches, DevelopmentIntakeFixtures intake,
    DevelopmentInspectionFixtures inspections, DevelopmentListingFixtures listings, IPasswordHasher<AccountUser> passwords)
{
    public async Task<IReadOnlyList<DevelopmentFixtureReport>> SeedAsync(DevelopmentFixtureTarget target,
        CancellationToken cancellationToken)
    {
        target.EnsureMatches(database.Database.GetConnectionString());
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(2));
        var token = deadline.Token;
        await using var lockConnection = await DevelopmentFixtureLock.AcquireAsync(target, token);
        // One bounded session avoids reopening unpooled connections for every canonical command/read.
        var opened = database.Database.GetDbConnection().State != System.Data.ConnectionState.Open;
        if (opened)
        {
            await database.Database.OpenConnectionAsync(token);
        }
        try
        {
            await intake.EnsureLocalStorageAsync(token);

            // A dedicated nonpooled connection owns the run lock; disposal releases it after failure or restart.
            var reports = new List<DevelopmentFixtureReport>();
            foreach (var branch in DevelopmentFixtureManifest.Branches)
            {
                var provisioned = await branches.ProvisionBranchAsync(branch, token);
                reports.Add(Report(branch.Name, provisioned.Created, provisioned.MatchesRequestedState));
            }

            foreach (var customer in DevelopmentFixtureManifest.Customers)
            {
                reports.Add(await customers.SeedAsync(customer, token));
            }

            foreach (var account in DevelopmentFixtureManifest.Staff)
            {
                var provisioned = await staff.ProvisionStaffAsync(account, token);
                reports.Add(Report($"{account.Role} ({account.Email})", provisioned.Created, provisioned.MatchesRequestedState));
            }

            reports.AddRange(await intake.SeedAsync(target, token));
            reports.AddRange(await inspections.RunAsync(true, token));
            reports.AddRange(await listings.RunAsync(true, token));
            return reports;
        }
        finally
        {
            if (opened)
            {
                await database.Database.CloseConnectionAsync();
            }
        }
    }

    public async Task<IReadOnlyList<DevelopmentFixtureReport>> ReviewAsync(DevelopmentFixtureTarget target,
        CancellationToken cancellationToken)
    {
        target.EnsureMatches(database.Database.GetConnectionString());
        // One bounded session avoids reopening unpooled connections for every canonical command/read.
        var opened = database.Database.GetDbConnection().State != System.Data.ConnectionState.Open;
        if (opened)
        {
            await database.Database.OpenConnectionAsync(cancellationToken);
        }
        try
        {
            var reports = new List<DevelopmentFixtureReport>();
            foreach (var requested in DevelopmentFixtureManifest.Branches)
            {
                var branch = await database.Branches.AsNoTracking().SingleOrDefaultAsync(
                    candidate => candidate.Id == requested.Id, cancellationToken);
                var matches = branch is not null && BranchProvisioningStore.Matches(branch, requested);
                var state = branch is null ? "missing" : "drift; preserved";
                reports.Add(new DevelopmentFixtureReport(requested.Name, matches ? "matches" : state, !matches));
            }

            foreach (var customer in DevelopmentFixtureManifest.Customers)
            {
                reports.Add(await customers.ReviewAsync(customer, cancellationToken));
            }

            foreach (var account in DevelopmentFixtureManifest.Staff)
            {
                reports.Add(await ReviewAccountAsync(account, cancellationToken));
            }

            reports.AddRange(await intake.ReviewAsync(cancellationToken));
            reports.AddRange(await inspections.RunAsync(false, cancellationToken));
            reports.AddRange(await listings.RunAsync(false, cancellationToken));
            return reports;
        }
        finally
        {
            if (opened)
            {
                await database.Database.CloseConnectionAsync();
            }
        }
    }

    private async Task<DevelopmentFixtureReport> ReviewAccountAsync(StaffProvisioningRequest requested,
        CancellationToken cancellationToken)
    {
        var account = await database.Users.AsNoTracking().SingleOrDefaultAsync(
            candidate => candidate.Id == requested.Id, cancellationToken);
        var staff = new ValidatedStaffProvisioning(requested.Id, requested.Email, requested.Password,
            requested.Role, requested.BranchId);
        var matches = account is not null && StaffProvisioningStore.Matches(account, staff, passwords);
        var state = account is null ? "missing" : "drift; preserved";
        return new DevelopmentFixtureReport($"{requested.Role} ({requested.Email})", matches ? "matches" : state, !matches);
    }

    private static DevelopmentFixtureReport Report(string fixture, bool created, bool matches)
    {
        if (created)
        {
            return new DevelopmentFixtureReport(fixture, "created", false);
        }

        return new DevelopmentFixtureReport(fixture, matches ? "matches" : "drift; preserved", !matches);
    }
}

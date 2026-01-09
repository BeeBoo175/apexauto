using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Dealership.Application;
using Dealership.Application.Accounts;
using Dealership.Infrastructure;
using Dealership.Infrastructure.Development;
using Dealership.Infrastructure.Identity;
using Dealership.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace Dealership.IntegrationTests;

public sealed class DevelopmentFixtureTests(RegistrationDatabase fixture) : IClassFixture<RegistrationDatabase>
{
    [Fact]
    public async Task SeedResumesSerializesPreservesEditsAndAuthenticatesSixRolesAcrossThreeBranches()
    {
        var connection = fixture.RuntimeConnectionString;
        var databaseName = new NpgsqlConnectionStringBuilder(connection).Database!;
        var target = DevelopmentFixtureTarget.Validate("Development", databaseName, connection);
        var fault = new InterruptedSeed();
        await using var provider = CreateProvider(connection, fault);
        var unrelatedId = Guid.NewGuid();

        await using (var scope = provider.CreateAsyncScope())
        {
            var provisioner = scope.ServiceProvider.GetRequiredService<StaffProvisioningService>();
            await provisioner.ProvisionStaffAsync(new StaffProvisioningRequest(unrelatedId,
                $"hakim.ismail.{Guid.NewGuid():N}@example.invalid", "OtherPassword123!",
                AccountRoles.Administrator, null), CancellationToken.None);
            var runner = scope.ServiceProvider.GetRequiredService<DevelopmentFixtureRunner>();
            Assert.All(await runner.ReviewAsync(target, CancellationToken.None), report =>
            {
                Assert.Equal("missing", report.State);
                Assert.True(report.RequiresAttention);
            });
            await Assert.ThrowsAsync<IOException>(() => runner.SeedAsync(target, CancellationToken.None));
        }

        fault.Enabled = false;
        var competing = await Task.WhenAll(SeedAsync(provider, target), SeedAsync(provider, target));
        Assert.Equal(10, competing.SelectMany(reports => reports).Count(report => report.State == "created"));
        Assert.All(competing.SelectMany(reports => reports), report =>
        {
            Assert.True(report.State is "created" or "matches");
            Assert.False(report.RequiresAttention);
        });
        await AssertNormalAuthenticationAsync();

        string editedHash;
        string editedCustomerHash;
        await using (var scope = provider.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
            Assert.Equal(13, await database.Users.CountAsync());
            Assert.Equal(1, await database.CustomerProfiles.CountAsync());
            Assert.False((await database.Users.SingleAsync(user => user.Role == AccountRoles.Customer)).EmailConfirmed);
            Assert.Equal(3, await database.Branches.CountAsync());

            foreach (var branch in DevelopmentFixtureManifest.Branches)
            {
                var persisted = await database.Branches.SingleAsync(candidate => candidate.Id == branch.Id);
                Assert.Equal(branch.Name, persisted.Name);
                Assert.Equal(branch.City, persisted.City);
                Assert.Equal(branch.State, persisted.State);
                Assert.Equal(3, await database.Users.CountAsync(user => user.BranchId == branch.Id));
            }

            var account = await database.Users.SingleAsync(user => user.Id == DevelopmentFixtureManifest.Staff[0].Id);
            account.Role = AccountRoles.GeneralManager;
            account.BranchId = null;
            account.IsActive = false;
            editedHash = scope.ServiceProvider.GetRequiredService<IPasswordHasher<AccountUser>>()
                .HashPassword(account, "EditedPassword123!");
            account.PasswordHash = editedHash;
            var profile = await database.CustomerProfiles.SingleAsync();
            var customer = await database.Users.SingleAsync(user => user.Id == profile.UserId);
            editedCustomerHash = scope.ServiceProvider.GetRequiredService<IPasswordHasher<AccountUser>>()
                .HashPassword(customer, "EditedCustomer123!");
            customer.PasswordHash = editedCustomerHash;
            customer.EmailConfirmed = true;
            await database.SaveChangesAsync();
            await database.Branches.Where(branch => branch.Id == DevelopmentFixtureManifest.Branches[1].Id)
                .ExecuteUpdateAsync(update => update.SetProperty(branch => branch.Phone, "+60378759999"));
        }

        await using var restarted = CreateProvider(connection, fault);
        var replay = await SeedAsync(restarted, target);
        Assert.Equal(3, replay.Count(report => report.State == "drift; preserved"));
        Assert.Equal(3, replay.Count(report => report.RequiresAttention));
        await using var check = restarted.CreateAsyncScope();
        var saved = check.ServiceProvider.GetRequiredService<DealershipDbContext>();
        var retained = await saved.Users.SingleAsync(user => user.Id == DevelopmentFixtureManifest.Staff[0].Id);
        Assert.Equal(AccountRoles.GeneralManager, retained.Role);
        Assert.Null(retained.BranchId);
        Assert.False(retained.IsActive);
        Assert.Equal(editedHash, retained.PasswordHash);
        var retainedProfile = await saved.CustomerProfiles.SingleAsync();
        var retainedCustomer = await saved.Users.SingleAsync(user => user.Id == retainedProfile.UserId);
        Assert.Equal(editedCustomerHash, retainedCustomer.PasswordHash);
        Assert.True(retainedCustomer.EmailConfirmed);
        Assert.Equal("+60378759999", (await saved.Branches.SingleAsync(
            branch => branch.Id == DevelopmentFixtureManifest.Branches[1].Id)).Phone);
        Assert.True(await saved.Users.AnyAsync(user => user.Id == unrelatedId));
        Assert.Equal(13, await saved.Users.CountAsync());
        var before = await saved.AccountSessions.CountAsync();
        var review = await check.ServiceProvider.GetRequiredService<DevelopmentFixtureRunner>()
            .ReviewAsync(target, CancellationToken.None);
        Assert.Equal(15, review.Count);
        Assert.All(review, report => Assert.False(report.RequiresAttention));
        Assert.Equal(15, review.Select(report => report.Fixture).Distinct().Count());
        Assert.Equal(before, await saved.AccountSessions.CountAsync());
    }

    private async Task AssertNormalAuthenticationAsync()
    {
        // Separate hosts keep each normal process-local login budget below its limit.
        using var customerFactory = CreateFactory();
        using var customerClient = customerFactory.CreateClient();
        await AssertLoginAsync(customerClient, DevelopmentFixtureManifest.Customer.Email!, HttpStatusCode.OK);
        foreach (var accounts in DevelopmentFixtureManifest.Staff.Chunk(5))
        {
            using var staffFactory = CreateFactory();
            using var staffClient = staffFactory.CreateClient();

            foreach (var account in accounts)
            {
                await AssertLoginAsync(staffClient, account.Email, HttpStatusCode.Forbidden);
            }
        }
    }

    private WebApplicationFactory<Program> CreateFactory()
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<DealershipDbContext>>();
                services.RemoveAll<DealershipDbContext>();
                services.AddDbContext<DealershipDbContext>(options => options.UseNpgsql(fixture.RuntimeConnectionString));
            });
        });
    }

    private static async Task AssertLoginAsync(HttpClient client, string email, HttpStatusCode accountStatus)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(Guid.NewGuid(), email, DevelopmentFixtureManifest.Password));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var session = await response.Content.ReadFromJsonAsync<LoginSession>();
        Assert.NotNull(session);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        Assert.Equal(accountStatus, (await client.GetAsync("/api/account")).StatusCode);
        client.DefaultRequestHeaders.Authorization = null;
    }

    private static ServiceProvider CreateProvider(string connection, InterruptedSeed fault)
    {
        var configuration = new ConfigurationBuilder().AddEnvironmentVariables().AddInMemoryCollection(
            new Dictionary<string, string?> { ["ConnectionStrings:DefaultConnection"] = connection }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddInfrastructure(configuration);
        services.AddScoped<DevelopmentFixtureRunner>();
        services.AddScoped<IStaffProvisioningStore>(provider =>
            new InterruptingStore(ActivatorUtilities.CreateInstance<StaffProvisioningStore>(provider), fault));
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    private static async Task<IReadOnlyList<DevelopmentFixtureReport>> SeedAsync(ServiceProvider provider,
        DevelopmentFixtureTarget target)
    {
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<DevelopmentFixtureRunner>()
            .SeedAsync(target, CancellationToken.None);
    }

    private sealed class InterruptedSeed
    {
        public bool Enabled { get; set; } = true;
    }

    private sealed class InterruptingStore(StaffProvisioningStore inner, InterruptedSeed fault) : IStaffProvisioningStore
    {
        public Task<ProvisioningResult> ProvisionStaffAsync(ValidatedStaffProvisioning staff, CancellationToken cancellationToken)
        {
            if (fault.Enabled && staff.Role == AccountRoles.WorkshopTechnician)
            {
                throw new IOException("Injected process interruption after committed fixture units.");
            }

            return inner.ProvisionStaffAsync(staff, cancellationToken);
        }
    }
}

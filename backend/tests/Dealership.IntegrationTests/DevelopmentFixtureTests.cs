using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Dealership.Application;
using Dealership.Application.Accounts;
using Dealership.Application.Intake;
using Dealership.Infrastructure;
using Dealership.Infrastructure.Development;
using Dealership.Infrastructure.Identity;
using Dealership.Infrastructure.Configuration;
using Dealership.Infrastructure.Intake;
using Dealership.Infrastructure.Storage;
using Dealership.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace Dealership.IntegrationTests;

public sealed class DevelopmentFixtureTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task SeedResumesSerializesPreservesEditsAndAuthenticatesSixRolesAcrossThreeBranches()
    {
        var connection = fixture.Database.RuntimeConnectionString;
        var databaseName = new NpgsqlConnectionStringBuilder(connection).Database!;
        var target = DevelopmentFixtureTarget.Validate("Development", databaseName, connection);
        var fault = new DevelopmentFixtureInterruption();
        var clock = new FixtureClock(DateTimeOffset.UtcNow.AddHours(-25));
        await using var provider = CreateProvider(connection, fault, clock);
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
            var storage = scope.ServiceProvider.GetRequiredService<PhotoStorageSettings>();
            var endpoint = storage.ServiceURL;

            try
            {
                storage.ServiceURL = "https://example.invalid";
                await Assert.ThrowsAsync<InvalidOperationException>(() => runner.SeedAsync(target, CancellationToken.None));
                Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<DealershipDbContext>().Branches.CountAsync());
            }
            finally
            {
                storage.ServiceURL = endpoint;
            }

            await Assert.ThrowsAsync<IOException>(() => runner.SeedAsync(target, CancellationToken.None));
        }

        fault.InterruptConfirmation = false;
        clock.Advance(TimeSpan.FromHours(25));
        await Assert.ThrowsAsync<IOException>(() => SeedAsync(provider, target));
        fault.Enabled = false;
        await Assert.ThrowsAsync<IOException>(() => SeedAsync(provider, target));
        await DevelopmentIntakeFixtureAssertions.AssertInterruptedMediaAsync(provider);
        fault.InterruptPhoto = false;
        await using var resumed = CreateProvider(connection, fault, clock);
        var competing = await Task.WhenAll(SeedAsync(resumed, target), SeedAsync(resumed, target));
        Assert.Equal(123, competing.SelectMany(reports => reports).Count(report => report.State == "created"));
        Assert.All(competing.SelectMany(reports => reports), report =>
        {
            Assert.True(report.State is "created" or "matches", $"{report.Fixture}: {report.State}");
            Assert.False(report.RequiresAttention);
        });
        await AssertNormalAuthenticationAsync();
        await DevelopmentIntakeFixtureAssertions.VerifyAsync(provider);
        var intakeSnapshot = await DevelopmentIntakeFixtureAssertions.SnapshotAsync(provider);

        string editedHash;
        string editedCustomerHash;
        await using (var scope = provider.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
            Assert.Equal(16, await database.Users.CountAsync());
            Assert.Equal(4, await database.CustomerProfiles.CountAsync());
            Assert.Equal(5, await database.EmailVerificationChallenges.CountAsync());
            Assert.Equal(3, await database.EmailVerificationChallenges.CountAsync(challenge => challenge.ConsumedAt != null));
            Assert.Equal(1, await database.EmailVerificationChallenges.CountAsync(challenge => challenge.ResendKey != null));

            foreach (var customerFixture in DevelopmentFixtureManifest.Customers)
            {
                var user = await database.Users.SingleAsync(candidate => candidate.Email == customerFixture.Registration.Email);
                Assert.Equal(customerFixture.VerifyEmail, user.EmailConfirmed);
            }

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
            var profile = await database.CustomerProfiles.SingleAsync(candidate =>
                candidate.RegistrationKey == DevelopmentFixtureManifest.Customers[0].Registration.RegistrationKey);
            var customer = await database.Users.SingleAsync(user => user.Id == profile.UserId);
            editedCustomerHash = scope.ServiceProvider.GetRequiredService<IPasswordHasher<AccountUser>>()
                .HashPassword(customer, "EditedCustomer123!");
            customer.PasswordHash = editedCustomerHash;
            customer.EmailConfirmed = true;
            var seller = await database.Users.SingleAsync(candidate =>
                candidate.Email == DevelopmentFixtureManifest.Customers[1].Registration.Email);
            seller.EmailConfirmed = false;
            await database.SaveChangesAsync();
            await database.Branches.Where(branch => branch.Id == DevelopmentFixtureManifest.Branches[1].Id)
                .ExecuteUpdateAsync(update => update.SetProperty(branch => branch.Phone, "+60378759999"));
        }

        clock.Advance(TimeSpan.FromDays(2));
        await using var restarted = CreateProvider(connection, fault, clock);
        var replay = await SeedAsync(restarted, target);
        Assert.Equal(intakeSnapshot, await DevelopmentIntakeFixtureAssertions.SnapshotAsync(restarted));
        Assert.Equal(3, replay.Count(report => report.State == "drift; preserved"));
        Assert.Equal(4, replay.Count(report => report.RequiresAttention));
        Assert.Single(replay, report => report.State == "verification requires attention; preserved");
        await using var check = restarted.CreateAsyncScope();
        var saved = check.ServiceProvider.GetRequiredService<DealershipDbContext>();
        var retained = await saved.Users.SingleAsync(user => user.Id == DevelopmentFixtureManifest.Staff[0].Id);
        Assert.Equal(AccountRoles.GeneralManager, retained.Role);
        Assert.Null(retained.BranchId);
        Assert.False(retained.IsActive);
        Assert.Equal(editedHash, retained.PasswordHash);
        var retainedProfile = await saved.CustomerProfiles.SingleAsync(profile =>
            profile.RegistrationKey == DevelopmentFixtureManifest.Customers[0].Registration.RegistrationKey);
        var retainedCustomer = await saved.Users.SingleAsync(user => user.Id == retainedProfile.UserId);
        Assert.Equal(editedCustomerHash, retainedCustomer.PasswordHash);
        Assert.True(retainedCustomer.EmailConfirmed);
        Assert.False((await saved.Users.SingleAsync(candidate =>
            candidate.Email == DevelopmentFixtureManifest.Customers[1].Registration.Email)).EmailConfirmed);
        Assert.Equal(5, await saved.EmailVerificationChallenges.CountAsync());
        Assert.Equal("+60378759999", (await saved.Branches.SingleAsync(
            branch => branch.Id == DevelopmentFixtureManifest.Branches[1].Id)).Phone);
        Assert.True(await saved.Users.AnyAsync(user => user.Id == unrelatedId));
        Assert.Equal(16, await saved.Users.CountAsync());
        var before = await saved.AccountSessions.CountAsync();
        var review = await check.ServiceProvider.GetRequiredService<DevelopmentFixtureRunner>()
            .ReviewAsync(target, CancellationToken.None);
        Assert.Equal(141, review.Count);
        Assert.All(review, report => Assert.False(report.RequiresAttention));
        Assert.Equal(141, review.Select(report => report.Fixture).Distinct().Count());
        Assert.Equal(before, await saved.AccountSessions.CountAsync());
    }

    private async Task AssertNormalAuthenticationAsync()
    {
        // Separate hosts keep each normal process-local login budget below its limit.
        using var customerFactory = fixture.CreateFactory();
        using var customerClient = customerFactory.CreateClient();
        foreach (var customer in DevelopmentFixtureManifest.Customers)
        {
            await AssertLoginAsync(customerClient, customer.Registration.Email!, HttpStatusCode.OK);
        }

        foreach (var accounts in DevelopmentFixtureManifest.Staff.Chunk(5))
        {
            using var staffFactory = fixture.CreateFactory();
            using var staffClient = staffFactory.CreateClient();

            foreach (var account in accounts)
            {
                await AssertLoginAsync(staffClient, account.Email, HttpStatusCode.Forbidden);
            }
        }
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

    private ServiceProvider CreateProvider(string connection, DevelopmentFixtureInterruption fault, TimeProvider clock)
    {
        var configuration = new ConfigurationBuilder().AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"))
            .AddEnvironmentVariables().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connection,
                ["AWS:BucketName"] = fixture.BucketName,
                ["IntakePhotos:CleanupEnabled"] = "false"
            }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddInfrastructure(configuration);
        services.AddSingleton(InspectionScheduleConfiguration.Read(configuration));
        services.AddScoped<IntakeDiscoveryService>();
        services.AddScoped<IntakeBookingService>();
        services.AddS3Storage(configuration);
        services.AddIntakeMedia(configuration);
        services.RemoveAll<TimeProvider>();
        services.AddSingleton(clock);
        services.AddScoped<DevelopmentFixtureRunner>();
        services.AddScoped<DevelopmentCustomerFixtures>();
        services.AddScoped<DevelopmentIntakeFixtures>();
        services.AddScoped<DevelopmentFixturePhotos>();
        fault.Configure(services);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    private static async Task<IReadOnlyList<DevelopmentFixtureReport>> SeedAsync(ServiceProvider provider,
        DevelopmentFixtureTarget target)
    {
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<DevelopmentFixtureRunner>()
            .SeedAsync(target, CancellationToken.None);
    }

    private sealed class FixtureClock(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan elapsed)
        {
            _now += elapsed;
        }
    }
}

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Dealership.Application.Accounts;
using Dealership.Application.Branches;
using Dealership.Infrastructure.Identity;
using Dealership.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dealership.IntegrationTests;

public sealed class StaffSessionTests(RegistrationDatabase database) : IClassFixture<RegistrationDatabase>
{
    [Fact]
    public async Task StaffSessionFollowsCurrentAccountAndBranchStateAcrossRestart()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var firstBranch = await CreateBranchAsync(factory, "ApexAuto Glenmarie");
        var secondBranch = await CreateBranchAsync(factory, "ApexAuto Jalan Ipoh");
        var email = $"aina.sofia.{Guid.NewGuid():N}@example.invalid";
        var userId = await CreateStaffAsync(factory, email, AccountRoles.SalesAgent, firstBranch);
        var session = await LoginAsync(client, email);

        Assert.Equal(HttpStatusCode.Forbidden, await AccountStatusAsync(client, session));
        var actorResponse = await client.GetAsync("/api/auth/session");
        Assert.True(actorResponse.Headers.CacheControl?.NoStore);
        Assert.Equal(new CurrentActor(userId, AccountRoles.SalesAgent, firstBranch),
            await actorResponse.Content.ReadFromJsonAsync<CurrentActor>());
        await UpdateUserAsync(factory, userId, user => user.BranchId = secondBranch);
        Assert.Equal(HttpStatusCode.Forbidden, await AccountStatusAsync(client, session));
        Assert.Equal(new CurrentActor(userId, AccountRoles.SalesAgent, secondBranch),
            await client.GetFromJsonAsync<CurrentActor>("/api/auth/session"));

        await SetBranchActiveAsync(factory, secondBranch, false);
        Assert.Equal(HttpStatusCode.Unauthorized, await AccountStatusAsync(client, session));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/session")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(Guid.NewGuid(), email, "Password123!"))).StatusCode);

        await UpdateUserAsync(factory, userId, user =>
        {
            user.Role = AccountRoles.GeneralManager;
            user.BranchId = null;
        });
        Assert.Equal(HttpStatusCode.Forbidden, await AccountStatusAsync(client, session));
        Assert.Equal(new CurrentActor(userId, AccountRoles.GeneralManager, null),
            await client.GetFromJsonAsync<CurrentActor>("/api/auth/session"));
        await UpdateUserAsync(factory, userId, user =>
        {
            user.Role = AccountRoles.SalesAgent;
            user.BranchId = secondBranch;
        });

        using var restarted = CreateFactory();
        using var restartedClient = restarted.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, await AccountStatusAsync(restartedClient, session));
        Assert.Equal(HttpStatusCode.Unauthorized, (await restartedClient.GetAsync("/api/auth/session")).StatusCode);
        await SetBranchActiveAsync(restarted, secondBranch, true);
        Assert.Equal(HttpStatusCode.Forbidden, await AccountStatusAsync(restartedClient, session));
        await UpdateUserAsync(restarted, userId, user => user.IsActive = false);
        Assert.Equal(HttpStatusCode.Unauthorized, await AccountStatusAsync(restartedClient, session));
        Assert.Equal(HttpStatusCode.Unauthorized, (await restartedClient.GetAsync("/api/auth/session")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await restartedClient.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(Guid.NewGuid(), email, "Password123!"))).StatusCode);
        await UpdateUserAsync(restarted, userId, user => user.IsActive = true);
        Assert.Equal(HttpStatusCode.NoContent, (await restartedClient.PostAsync("/api/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await restartedClient.GetAsync("/api/auth/session")).StatusCode);
    }

    [Fact]
    public async Task GroupStaffCanLogInButCustomerAccountRemainsCustomerOnly()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var branchId = await CreateBranchAsync(factory, "ApexAuto Subang Jaya");
        var managerEmail = $"farah.aziz.{Guid.NewGuid():N}@example.invalid";
        await CreateStaffAsync(factory, managerEmail, AccountRoles.GeneralManager, null);
        var manager = await LoginAsync(client, managerEmail);
        await SetBranchActiveAsync(factory, branchId, false);
        Assert.Equal(HttpStatusCode.Forbidden, await AccountStatusAsync(client, manager));

        var registration = new RegistrationRequest(Guid.NewGuid(), "Nur Aisyah Rahman",
            $"nur.aisyah.{Guid.NewGuid():N}@example.invalid", "Password123!", "940215140865",
            "0123456789", "18 Jalan Melati, Petaling Jaya, Selangor", TestUsernames.Next());
        Assert.Equal(HttpStatusCode.NoContent,
            (await client.PostAsJsonAsync("/api/auth/register", registration)).StatusCode);
        var customer = await LoginAsync(client, registration.Email!);
        Assert.Equal(HttpStatusCode.OK, await AccountStatusAsync(client, customer));
        Assert.Equal(AccountRoles.Customer, (await client.GetFromJsonAsync<CurrentActor>("/api/auth/session"))!.Role);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        var user = await context.Users.SingleAsync(account => account.Email == registration.Email);
        user.Role = AccountRoles.Administrator;
        await context.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Forbidden, await AccountStatusAsync(client, customer));
        Assert.Equal(AccountRoles.Administrator, (await client.GetFromJsonAsync<CurrentActor>("/api/auth/session"))!.Role);
    }

    [Fact]
    public async Task OwnProfileShowsStaffWorkDetailsAndCustomerIdentity()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var branchId = await CreateBranchAsync(factory, "ApexAuto Cheras");
        var email = $"azlan.rahim.{Guid.NewGuid():N}@example.invalid";
        var userId = await CreateStaffAsync(factory, email, AccountRoles.SalesAgent, branchId);
        var staff = await LoginAsync(client, email);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", staff.AccessToken);

        var response = await client.GetAsync("/api/account/profile");
        Assert.True(response.Headers.CacheControl?.NoStore);
        var profile = (await response.Content.ReadFromJsonAsync<AccountProfile>())!;
        Assert.Equal(userId, profile.UserId);
        Assert.Equal(AccountRoles.SalesAgent, profile.Role);
        Assert.Equal("Azlan Rahim", profile.Name);
        Assert.Equal(email, profile.Email);
        Assert.Equal(branchId, profile.BranchId);
        Assert.Equal("ApexAuto Cheras", profile.BranchName);
        Assert.Equal("0123456707", profile.Mobile);
        Assert.True(UsernamePolicy.IsValid(profile.Username));

        var registration = new RegistrationRequest(Guid.NewGuid(), "Nur Aisyah Rahman",
            $"nur.aisyah.{Guid.NewGuid():N}@example.invalid", "Password123!", "950312101234",
            "0123456789", "18 Jalan Melati, Petaling Jaya, Selangor", TestUsernames.Next());
        Assert.Equal(HttpStatusCode.NoContent,
            (await client.PostAsJsonAsync("/api/auth/register", registration)).StatusCode);
        var customer = await LoginAsync(client, registration.Email!);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", customer.AccessToken);
        var own = (await client.GetFromJsonAsync<AccountProfile>("/api/account/profile"))!;
        Assert.Equal(AccountRoles.Customer, own.Role);
        Assert.Equal("Nur Aisyah Rahman", own.Name);
        Assert.Equal(registration.Username, own.Username);
        Assert.Null(own.BranchName);
        Assert.Null(own.Mobile);
    }

    private WebApplicationFactory<Program> CreateFactory()
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<DealershipDbContext>>();
                services.RemoveAll<DealershipDbContext>();
                services.AddDbContext<DealershipDbContext>(options => options.UseNpgsql(database.RuntimeConnectionString));
            });
        });
    }

    private static async Task<Guid> CreateBranchAsync(WebApplicationFactory<Program> factory, string name)
    {
        using var scope = factory.Services.CreateScope();
        var provisioner = scope.ServiceProvider.GetRequiredService<BranchProvisioningService>();
        var branchId = Guid.NewGuid();
        var result = await provisioner.ProvisionBranchAsync(new BranchProvisioningRequest(branchId, name,
            "12 Jalan Pengaturcara U1/51A", "Shah Alam", "Selangor", "+60355651234"), CancellationToken.None);
        Assert.True(result.Created);
        return branchId;
    }

    private static async Task<Guid> CreateStaffAsync(WebApplicationFactory<Program> factory, string email, string role,
        Guid? branchId)
    {
        using var scope = factory.Services.CreateScope();
        var provisioner = scope.ServiceProvider.GetRequiredService<StaffProvisioningService>();
        var staffId = Guid.NewGuid();
        var result = await provisioner.ProvisionStaffAsync(new StaffProvisioningRequest(staffId, email,
            "Password123!", role, branchId, "Azlan Rahim", "0123456707"), CancellationToken.None);
        Assert.True(result.Created);
        return staffId;
    }

    private static async Task<LoginSession> LoginAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(Guid.NewGuid(), email, "Password123!"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<LoginSession>())!;
    }

    private static async Task<HttpStatusCode> AccountStatusAsync(HttpClient client, LoginSession session)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        return (await client.GetAsync("/api/account")).StatusCode;
    }

    private static async Task UpdateUserAsync(WebApplicationFactory<Program> factory, Guid userId,
        Action<AccountUser> change)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        var user = await context.Users.SingleAsync(account => account.Id == userId);
        change(user);
        await context.SaveChangesAsync();
    }

    private static async Task SetBranchActiveAsync(WebApplicationFactory<Program> factory, Guid branchId, bool active)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        await context.Branches.Where(branch => branch.Id == branchId)
            .ExecuteUpdateAsync(update => update.SetProperty(branch => branch.IsActive, active));
    }
}

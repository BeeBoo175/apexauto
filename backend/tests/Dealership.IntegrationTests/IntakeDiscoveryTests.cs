using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Dealership.Application.Accounts;
using Dealership.Application.Intake;
using Dealership.Infrastructure.Development;
using Dealership.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dealership.IntegrationTests;

public sealed class IntakeDiscoveryTests(RegistrationDatabase database) : IClassFixture<RegistrationDatabase>
{
    [Fact]
    public async Task PublicPagesAndSlotOffersUseCurrentActiveBranchesAndBoundedInputs()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        Guid branchId;
        using (var scope = factory.Services.CreateScope())
        {
            var provisioning = scope.ServiceProvider.GetRequiredService<StaffProvisioningService>();
            var branch = DevelopmentFixtureManifest.Branch;
            var branches = new[]
            {
                branch,
                branch with { Id = Guid.NewGuid(), Name = "ApexAuto Petaling Jaya", Address = "22 Jalan SS 2/24", City = "Petaling Jaya" },
                branch with { Id = Guid.NewGuid(), Name = "ApexAuto Bayan Lepas", Address = "18 Jalan Sultan Azlan Shah", City = "Bayan Lepas", State = "Pulau Pinang" },
                branch with { Id = Guid.NewGuid(), Name = "ApexAuto Klang", Address = "8 Jalan Meru", City = "Klang" },
                branch with { Id = Guid.NewGuid(), Name = "ApexAuto Cheras", Address = "16 Jalan Cheras", City = "Cheras", State = "Kuala Lumpur" },
                branch with { Id = Guid.NewGuid(), Name = "ApexAuto Subang Jaya", Address = "10 Jalan SS 15/4", City = "Subang Jaya" }
            };
            branchId = branch.Id;

            foreach (var request in branches)
            {
                await provisioning.ProvisionBranchAsync(request, CancellationToken.None);
            }

            var context = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
            await context.Branches.Where(candidate => candidate.Name == "ApexAuto Subang Jaya")
                .ExecuteUpdateAsync(update => update.SetProperty(candidate => candidate.IsActive, false));
        }

        var pages = new List<PublicBranchPage>();

        for (var page = 1; page <= 3; page++)
        {
            pages.Add((await client.GetFromJsonAsync<PublicBranchPage>($"/api/branches?page={page}&pageSize=2"))!);
        }

        Assert.All(pages, page => Assert.Equal(5, page.Total));
        Assert.Equal([2, 2, 1], pages.Select(page => page.Items.Count));
        var items = pages.SelectMany(page => page.Items).ToArray();
        Assert.Equal(5, items.Select(branch => branch.Id).Distinct().Count());
        Assert.Equal(items.Select(branch => branch.Name).Order(StringComparer.Ordinal), items.Select(branch => branch.Name));
        Assert.DoesNotContain(items, branch => branch.Name == "ApexAuto Subang Jaya");
        var beyond = await client.GetFromJsonAsync<PublicBranchPage>("/api/branches?page=2147483647&pageSize=50");
        Assert.Empty(beyond!.Items);
        Assert.Equal(5, beyond.Total);

        foreach (var query in new[] { "page=0", "pageSize=51", "pageSize=0", "page=oops", "pageSize=" })
        {
            var denied = await client.GetAsync("/api/branches?" + query);
            Assert.Equal(HttpStatusCode.BadRequest, denied.StatusCode);
            Assert.True(denied.Headers.CacheControl?.NoStore);
        }

        var route = $"/api/branches/{branchId}/inspection-slots";
        var response = await client.GetAsync(route + "?date=2026-10-15");
        Assert.True(response.Headers.CacheControl?.NoStore);
        var offer = (await response.Content.ReadFromJsonAsync<InspectionSlotOffer>())!;
        Assert.Equal(6, offer.Slots.Count);
        Assert.Equal(branchId, offer.BranchId);
        Assert.Equal(new DateOnly(2026, 11, 13), offer.DateWindow.LastDate);
        var sunday = (await client.GetFromJsonAsync<InspectionSlotOffer>(route + "?date=2026-10-18"))!;
        Assert.Empty(sunday.Slots);

        foreach (var date in new[] { "", "15-10-2026", "2026-02-30", "2026-10-14", "2026-11-14" })
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(route + "?date=" + date)).StatusCode);
        }

        var registration = DevelopmentFixtureManifest.Customer;
        using (var scope = factory.Services.CreateScope())
        {
            Assert.Equal(RegistrationStatus.Succeeded, (await scope.ServiceProvider.GetRequiredService<RegistrationService>()
                .RegisterAsync(registration, CancellationToken.None)).Status);
        }

        using var unverifiedFactory = CreateFactory(fixedClock: false);
        using var unverifiedClient = unverifiedFactory.CreateClient();
        var login = await unverifiedClient.PostAsJsonAsync("/api/auth/login", new LoginRequest(Guid.NewGuid(), registration.Email, registration.Password));
        var session = (await login.Content.ReadFromJsonAsync<LoginSession>())!;
        unverifiedClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        var account = (await unverifiedClient.GetFromJsonAsync<CustomerAccount>("/api/account"))!;
        Assert.False(account.EmailVerified);
        var currentDate = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow,
            TimeZoneInfo.FindSystemTimeZoneById("Asia/Kuala_Lumpur")).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(HttpStatusCode.OK, (await unverifiedClient.GetAsync(route + "?date=" + currentDate)).StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
            await context.Branches.Where(branch => branch.Id == branchId)
                .ExecuteUpdateAsync(update => update.SetProperty(branch => branch.IsActive, false));
        }

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(route + "?date=2026-10-15")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/branches/{Guid.NewGuid()}/inspection-slots?date=2026-10-15")).StatusCode);
        Assert.Equal(4, (await client.GetFromJsonAsync<PublicBranchPage>("/api/branches"))!.Total);
    }

    private WebApplicationFactory<Program> CreateFactory(bool fixedClock = true)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["Email:Mode"] = "Disabled" }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<DealershipDbContext>>();
                services.RemoveAll<DealershipDbContext>();
                services.AddDbContext<DealershipDbContext>(options => options.UseNpgsql(database.RuntimeConnectionString));
                if (fixedClock)
                {
                    services.RemoveAll<TimeProvider>();
                    services.AddSingleton<TimeProvider>(new MorningClock());
                }
            });
        });
    }

    private sealed class MorningClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return new DateTimeOffset(2026, 10, 14, 23, 0, 0, TimeSpan.Zero);
        }
    }
}

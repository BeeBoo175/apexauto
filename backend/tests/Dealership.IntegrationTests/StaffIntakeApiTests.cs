using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dealership.Application.Accounts;
using Dealership.Application.Branches;
using Dealership.Application.Intake;
using Dealership.Infrastructure.Development;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dealership.IntegrationTests;

public sealed class StaffIntakeApiTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    private const string Route = "/api/staff/intake/appointments";

    [Fact]
    public async Task NormalAuthenticationBindsEvidenceMapsOutcomesAndReconcilesPersistedReceipts()
    {
        using var data = await CheckInTestData.CreateAsync(fixture);
        var appointment = await data.BookAsync();
        var duplicateVin = await data.BookAsync(1, "BPP 1234");
        var duplicatePlate = await data.BookAsync(2, "WYY8821");
        using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var path = $"{Route}/{appointment.Id}/check-in";
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Route)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(path, CheckInTestData.Evidence)).StatusCode);
        await LoginAsync(client, data.StaffEmail);
        var pageResponse = await client.GetAsync(Route);
        Assert.True(pageResponse.Headers.CacheControl?.NoStore);
        var page = (await pageResponse.Content.ReadFromJsonAsync<StaffWorklistPage>())!;
        Assert.Equal(3, page.Total);
        Assert.Equal(20, page.PageSize);
        Assert.All(page.Items, item => Assert.Equal(data.BranchId, item.BranchId));
        var json = await pageResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("nric", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("address", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(data.CustomerEmail, json, StringComparison.OrdinalIgnoreCase);

        foreach (var query in new[] { "from=2026-10-16", "from=bad&to=bad", "status=0", "status=booked",
            "page=0", "page=abc", "pageSize=51", "branchId=bad", $"branchId={Guid.Empty}" })
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"{Route}?{query}")).StatusCode);
        }

        var evidence = CheckInTestData.Evidence with { MileageKilometres = 0 };
        var body = JsonSerializer.SerializeToNode(evidence, JsonSerializerOptions.Web)!.AsObject();
        body.Remove("mileageKilometres");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path, body)).StatusCode);
        body["mileageKilometres"] = null;
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path, body)).StatusCode);
        body["mileageKilometres"] = 1.5;
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path, body)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync(path, evidence with { CustomerPresent = false })).StatusCode);
        using var malformed = new StringContent("{", Encoding.UTF8, "application/json");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync(path, malformed)).StatusCode);
        using var oversized = new StringContent(new string(' ', 17000), Encoding.UTF8, "application/json");
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (await client.PostAsync(path, oversized)).StatusCode);
        using var wrongType = new StringContent("{}");
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, (await client.PostAsync(path, wrongType)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.PostAsJsonAsync($"{Route}/{Guid.NewGuid()}/check-in", evidence)).StatusCode);

        var responses = await Task.WhenAll(client.PostAsJsonAsync(path, evidence), client.PostAsJsonAsync(path,
            evidence with { Vin = " pl1bt65r84b123456 ", Notes = " Owner present " }));
        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        Assert.All(responses, response => Assert.True(response.Headers.CacheControl?.NoStore));
        var receipt = (await responses[0].Content.ReadFromJsonAsync<CheckInReceipt>())!;
        Assert.Equal(receipt, await responses[1].Content.ReadFromJsonAsync<CheckInReceipt>());
        Assert.Equal(evidence, receipt.Evidence);
        Assert.Equal(data.StaffId, receipt.StaffId);
        Assert.Equal(appointment.Id, receipt.AppointmentId);
        Assert.Equal(HttpStatusCode.Conflict,
            (await client.PostAsJsonAsync(path, evidence with { MileageKilometres = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await client.PostAsJsonAsync($"{Route}/{duplicateVin.Id}/check-in", evidence)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"{Route}/{duplicatePlate.Id}/check-in",
            evidence with { Vin = "PL1BT65R84B123457" })).StatusCode);
        var checkedIn = (await client.GetFromJsonAsync<StaffWorklistPage>($"{Route}?status=CheckedIn"))!;
        Assert.Equal(appointment.Id, Assert.Single(checkedIn.Items).Id);
        Assert.Equal(2, (await client.GetFromJsonAsync<StaffWorklistPage>(Route))!.Total);

        using var restarted = fixture.CreateFactory();
        using var reconciler = restarted.CreateClient();
        reconciler.DefaultRequestHeaders.Authorization = client.DefaultRequestHeaders.Authorization;
        Assert.Equal(receipt, await (await reconciler.PostAsJsonAsync(path, evidence)).Content.ReadFromJsonAsync<CheckInReceipt>());
        using var customer = restarted.CreateClient();
        await LoginAsync(customer, data.CustomerEmail);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync(Route)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.PostAsJsonAsync(path, evidence)).StatusCode);
        using var scope = restarted.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        Assert.Single(await database.AppointmentCheckIns.ToArrayAsync());
        Assert.Single(await database.AppointmentCheckInMails.ToArrayAsync());
        Assert.Equal(0, (await database.Vehicles.SingleAsync()).MileageKilometres);
        Assert.Equal(90000, (await database.SellerValuations.SingleAsync(item => item.AppointmentId == appointment.Id)).MileageKilometres);
        await AssertCurrentScopeAsync(reconciler, database, data, scope.ServiceProvider, path, evidence);
    }

    private static async Task AssertCurrentScopeAsync(HttpClient client, DealershipDbContext database, CheckInTestData data,
        IServiceProvider services, string path, Dealership.Domain.Intake.CheckInEvidence evidence)
    {
        foreach (var role in new[] { AccountRoles.WorkshopTechnician, AccountRoles.Administrator, AccountRoles.GeneralManager })
        {
            var branchId = role == AccountRoles.WorkshopTechnician ? (Guid?)data.BranchId : null;
            await database.Users.Where(user => user.Id == data.StaffId).ExecuteUpdateAsync(update => update
                .SetProperty(user => user.Role, role).SetProperty(user => user.BranchId, branchId));
            var expected = role == AccountRoles.GeneralManager ? HttpStatusCode.OK : HttpStatusCode.Forbidden;
            Assert.Equal(expected, (await client.GetAsync(Route)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(path, evidence)).StatusCode);
        }

        var otherBranch = Guid.NewGuid();
        await services.GetRequiredService<BranchProvisioningService>().ProvisionBranchAsync(
            DevelopmentFixtureManifest.Branch with { Id = otherBranch, Name = "ApexAuto Petaling Jaya" }, CancellationToken.None);
        var filtered = (await client.GetFromJsonAsync<StaffWorklistPage>($"{Route}?branchId={otherBranch}"))!;
        Assert.Equal(0, filtered.Total);
        await database.Users.Where(user => user.Id == data.StaffId).ExecuteUpdateAsync(update => update
            .SetProperty(user => user.Role, AccountRoles.BranchManager).SetProperty(user => user.BranchId, data.BranchId));
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(path, evidence)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"{Route}?branchId={otherBranch}")).StatusCode);
        await database.Users.Where(user => user.Id == data.StaffId)
            .ExecuteUpdateAsync(update => update.SetProperty(user => user.BranchId, otherBranch));
        Assert.Equal(0, (await client.GetFromJsonAsync<StaffWorklistPage>(Route))!.Total);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync(path, evidence)).StatusCode);
        await database.Users.Where(user => user.Id == data.StaffId)
            .ExecuteUpdateAsync(update => update.SetProperty(user => user.BranchId, data.BranchId));
        await database.Branches.Where(branch => branch.Id == data.BranchId)
            .ExecuteUpdateAsync(update => update.SetProperty(branch => branch.IsActive, false));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Route)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(path, evidence)).StatusCode);
        await database.Branches.Where(branch => branch.Id == data.BranchId)
            .ExecuteUpdateAsync(update => update.SetProperty(branch => branch.IsActive, true));
        await database.Users.Where(user => user.Id == data.StaffId)
            .ExecuteUpdateAsync(update => update.SetProperty(user => user.IsActive, false));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Route)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(path, evidence)).StatusCode);
    }

    private static async Task LoginAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(Guid.NewGuid(), email, "Password123!"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var session = (await response.Content.ReadFromJsonAsync<LoginSession>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
    }
}

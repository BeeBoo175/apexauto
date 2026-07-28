using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Dealership.Application.Accounts;
using Dealership.Domain.Accounts;
using Dealership.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dealership.IntegrationTests;

public sealed class CustomerSupportTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task RevealingTheNricNeedsAReasonIsRecordedOnceAndIsDeniedToTheCustomer()
    {
        using var scenario = await SupportScenario.StartAsync(fixture);
        var path = scenario.Path;

        var invalid = await scenario.Admin.PostAsJsonAsync($"{path}/nric-reveals",
            new { operationId = Guid.NewGuid(), reason = " " });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.True(invalid.Headers.CacheControl?.NoStore);
        Assert.Equal(0, await EventCountAsync(scenario.Factory, scenario.Id, AccountEventKind.NricRevealed));

        var reveal = new { operationId = Guid.NewGuid(), reason = "Caller asked to confirm the number on file" };
        var shown = await scenario.Admin.PostAsJsonAsync($"{path}/nric-reveals", reveal);
        Assert.True(shown.Headers.CacheControl?.NoStore);
        Assert.Equal(scenario.Registration.Nric, (await shown.Content.ReadFromJsonAsync<JsonObject>())!["nric"]!.GetValue<string>());
        await scenario.Admin.PostAsJsonAsync($"{path}/nric-reveals", reveal);
        Assert.Equal(1, await EventCountAsync(scenario.Factory, scenario.Id, AccountEventKind.NricRevealed));
        var denied = await scenario.Customer.PostAsJsonAsync($"{path}/nric-reveals",
            new { operationId = Guid.NewGuid(), reason = "Peeking" });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.True(denied.Headers.CacheControl?.NoStore);
    }

    [Fact]
    public async Task ResendingVerificationIsLoggedOnceForAReplayedOperation()
    {
        using var scenario = await SupportScenario.StartAsync(fixture);

        var verify = new { operationId = Guid.NewGuid(), reason = "Customer cannot find the email" };
        var resent = await scenario.Admin.PostAsJsonAsync($"{scenario.Path}/verification-resends", verify);
        Assert.Equal(HttpStatusCode.NoContent, resent.StatusCode);
        Assert.True(resent.Headers.CacheControl?.NoStore);
        Assert.Equal(HttpStatusCode.NoContent,
            (await scenario.Admin.PostAsJsonAsync($"{scenario.Path}/verification-resends", verify)).StatusCode);
        Assert.Equal(1, await EventCountAsync(scenario.Factory, scenario.Id, AccountEventKind.VerificationResent));
    }

    [Fact]
    public async Task RevokingSessionsSignsTheCustomerOutAndIsLogged()
    {
        using var scenario = await SupportScenario.StartAsync(fixture);

        var revoked = await scenario.Admin.PostAsJsonAsync($"{scenario.Path}/session-revocations",
            new { operationId = Guid.NewGuid(), reason = "Shared device" });
        Assert.Equal(HttpStatusCode.NoContent, revoked.StatusCode);
        Assert.True(revoked.Headers.CacheControl?.NoStore);
        Assert.Equal(HttpStatusCode.Unauthorized, (await scenario.Customer.GetAsync("/api/account/profile")).StatusCode);
        Assert.Equal(1, await EventCountAsync(scenario.Factory, scenario.Id, AccountEventKind.SessionsRevoked));
    }

    [Fact]
    public async Task SendingAPasswordResetIsLoggedAndAnUnknownCustomerIsNotFound()
    {
        using var scenario = await SupportScenario.StartAsync(fixture);

        var reset = await scenario.Admin.PostAsJsonAsync($"{scenario.Path}/password-resets",
            new { operationId = Guid.NewGuid(), reason = "Locked out" });
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        Assert.True(reset.Headers.CacheControl?.NoStore);
        Assert.Equal(1, await EventCountAsync(scenario.Factory, scenario.Id, AccountEventKind.ResetSent));
        Assert.Equal(HttpStatusCode.NotFound,
            (await scenario.Admin.PostAsJsonAsync($"/api/customer-accounts/{Guid.NewGuid()}/password-resets",
                new { operationId = Guid.NewGuid(), reason = "Locked out" })).StatusCode);
    }

    [Fact]
    public async Task SupportHistoryListsEveryActionWithoutTheRevealedNric()
    {
        using var scenario = await SupportScenario.StartAsync(fixture);

        Assert.Equal(HttpStatusCode.OK, (await scenario.Admin.PostAsJsonAsync($"{scenario.Path}/nric-reveals",
            new { operationId = Guid.NewGuid(), reason = "Caller asked to confirm the number on file" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await scenario.Admin.PostAsJsonAsync($"{scenario.Path}/verification-resends",
            new { operationId = Guid.NewGuid(), reason = "Customer cannot find the email" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await scenario.Admin.PostAsJsonAsync($"{scenario.Path}/session-revocations",
            new { operationId = Guid.NewGuid(), reason = "Shared device" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await scenario.Admin.PostAsJsonAsync($"{scenario.Path}/password-resets",
            new { operationId = Guid.NewGuid(), reason = "Locked out" })).StatusCode);

        var historyPath = $"/api/staff-accounts/{scenario.Id}/history";
        var history = (await scenario.Admin.GetFromJsonAsync<AccountEventPage>(historyPath))!;
        Assert.Equal(4, history.Items.Count);
        Assert.DoesNotContain(scenario.Registration.Nric!, await scenario.Admin.GetStringAsync(historyPath));
    }

    private static async Task<Guid> CustomerIdAsync(WebApplicationFactory<Program> factory, string email)
    {
        using var scope = factory.Services.CreateScope();
        var normalized = email.ToUpperInvariant();
        return await scope.ServiceProvider.GetRequiredService<DealershipDbContext>().Users
            .Where(user => user.NormalizedEmail == normalized).Select(user => user.Id).SingleAsync();
    }

    private static async Task<int> EventCountAsync(WebApplicationFactory<Program> factory, Guid targetId, AccountEventKind kind)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<DealershipDbContext>().AccountEvents
            .CountAsync(item => item.TargetId == targetId && item.Kind == kind);
    }

    private sealed class SupportScenario : IDisposable
    {
        private SupportScenario(WebApplicationFactory<Program> factory, HttpClient admin, HttpClient customer,
            RegistrationRequest registration, Guid id)
        {
            Factory = factory;
            Admin = admin;
            Customer = customer;
            Registration = registration;
            Id = id;
        }

        public WebApplicationFactory<Program> Factory { get; }
        public HttpClient Admin { get; }
        public HttpClient Customer { get; }
        public RegistrationRequest Registration { get; }
        public Guid Id { get; }
        public string Path => $"/api/customer-accounts/{Id}";

        public static async Task<SupportScenario> StartAsync(IntakePhotoFixture fixture)
        {
            var factory = fixture.CreateFactory();
            var admin = factory.CreateClient();
            await StaffInvitationTests.SignInAdministratorAsync(factory, admin);
            var customer = factory.CreateClient();
            var registration = await IntakePhotoFixture.RegisterAsync(customer);
            return new SupportScenario(factory, admin, customer, registration,
                await CustomerIdAsync(factory, registration.Email!));
        }

        public void Dispose()
        {
            Customer.Dispose();
            Admin.Dispose();
            Factory.Dispose();
        }
    }
}

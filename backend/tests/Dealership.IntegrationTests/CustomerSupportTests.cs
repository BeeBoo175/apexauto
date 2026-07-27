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
    public async Task SupportActionsNeedAReasonAreLoggedAndRevealingTheNricIsRecordedFirst()
    {
        using var factory = fixture.CreateFactory();
        using var admin = factory.CreateClient();
        await StaffInvitationTests.SignInAdministratorAsync(factory, admin);
        using var customer = factory.CreateClient();
        var registration = await IntakePhotoFixture.RegisterAsync(customer);
        var id = await CustomerIdAsync(factory, registration.Email!);
        var path = $"/api/customer-accounts/{id}";

        var invalid = await admin.PostAsJsonAsync($"{path}/nric-reveals",
            new { operationId = Guid.NewGuid(), reason = " " });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.True(invalid.Headers.CacheControl?.NoStore);
        Assert.Equal(0, await EventCountAsync(factory, id, AccountEventKind.NricRevealed));

        var reveal = new { operationId = Guid.NewGuid(), reason = "Caller asked to confirm the number on file" };
        var shown = await admin.PostAsJsonAsync($"{path}/nric-reveals", reveal);
        Assert.True(shown.Headers.CacheControl?.NoStore);
        Assert.Equal(registration.Nric, (await shown.Content.ReadFromJsonAsync<JsonObject>())!["nric"]!.GetValue<string>());
        await admin.PostAsJsonAsync($"{path}/nric-reveals", reveal);
        Assert.Equal(1, await EventCountAsync(factory, id, AccountEventKind.NricRevealed));
        var denied = await customer.PostAsJsonAsync($"{path}/nric-reveals",
            new { operationId = Guid.NewGuid(), reason = "Peeking" });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.True(denied.Headers.CacheControl?.NoStore);

        var verify = new { operationId = Guid.NewGuid(), reason = "Customer cannot find the email" };
        var resent = await admin.PostAsJsonAsync($"{path}/verification-resends", verify);
        Assert.Equal(HttpStatusCode.NoContent, resent.StatusCode);
        Assert.True(resent.Headers.CacheControl?.NoStore);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsJsonAsync($"{path}/verification-resends", verify)).StatusCode);
        Assert.Equal(1, await EventCountAsync(factory, id, AccountEventKind.VerificationResent));

        var revoked = await admin.PostAsJsonAsync($"{path}/session-revocations",
            new { operationId = Guid.NewGuid(), reason = "Shared device" });
        Assert.Equal(HttpStatusCode.NoContent, revoked.StatusCode);
        Assert.True(revoked.Headers.CacheControl?.NoStore);
        Assert.Equal(HttpStatusCode.Unauthorized, (await customer.GetAsync("/api/account/profile")).StatusCode);
        Assert.Equal(1, await EventCountAsync(factory, id, AccountEventKind.SessionsRevoked));

        var reset = await admin.PostAsJsonAsync($"{path}/password-resets",
            new { operationId = Guid.NewGuid(), reason = "Locked out" });
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        Assert.True(reset.Headers.CacheControl?.NoStore);
        Assert.Equal(1, await EventCountAsync(factory, id, AccountEventKind.ResetSent));
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsJsonAsync($"/api/customer-accounts/{Guid.NewGuid()}/password-resets",
            new { operationId = Guid.NewGuid(), reason = "Locked out" })).StatusCode);

        var history = (await admin.GetFromJsonAsync<AccountEventPage>($"/api/staff-accounts/{id}/history"))!;
        Assert.Equal(4, history.Items.Count);
        Assert.DoesNotContain(registration.Nric!, await admin.GetStringAsync($"/api/staff-accounts/{id}/history"));
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
}

using System.Net;
using System.Net.Http.Json;
using Dealership.Application.Accounts;
using Dealership.Domain.Accounts;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dealership.IntegrationTests;

public sealed class AccountSuspensionTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task SuspendingCutsAccessAtOnceBlocksRecoveryAndLiftingRestoresTheAccount()
    {
        using var factory = fixture.CreateFactory();
        using var admin = factory.CreateClient();
        await StaffInvitationTests.SignInAdministratorAsync(factory, admin);
        using var customer = factory.CreateClient();
        var registration = await IntakePhotoFixture.RegisterAsync(customer);
        Assert.NotNull(registration.Email);
        var customerId = await CustomerIdAsync(factory, registration.Email);
        Assert.Equal(HttpStatusCode.OK, (await customer.GetAsync("/api/account/profile")).StatusCode);

        var path = $"/api/accounts/{customerId}/suspensions";
        var invalid = await admin.PostAsJsonAsync(path, Request(Guid.NewGuid(), null));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.True(invalid.Headers.CacheControl?.NoStore);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync(path, Request(Guid.Empty, "Chargeback"))).StatusCode);
        var denied = await customer.PostAsJsonAsync(path, Request(Guid.NewGuid(), "Chargeback"));
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.True(denied.Headers.CacheControl?.NoStore);

        var operation = Guid.NewGuid();
        var suspendedResponse = await admin.PostAsJsonAsync(path, Request(operation, "Chargeback dispute", reset: true));
        Assert.Equal(HttpStatusCode.NoContent, suspendedResponse.StatusCode);
        Assert.True(suspendedResponse.Headers.CacheControl?.NoStore);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsJsonAsync(path, Request(operation, "Chargeback dispute", reset: true))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync(path, Request(Guid.NewGuid(), "Again"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await customer.GetAsync("/api/account/profile")).StatusCode);

        using var visitor = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await visitor.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(Guid.NewGuid(), registration.Email, registration.Password))).StatusCode);
        Assert.Equal(1, await EventCountAsync(factory, customerId, AccountEventKind.Suspended));
        Assert.False(await HasRecoveryChallengeAsync(factory, registration.Email));
        await visitor.PostAsJsonAsync("/api/auth/password-recovery", new { email = registration.Email });
        Assert.False(await HasRecoveryChallengeAsync(factory, registration.Email));

        var lift = $"{path}/lift";
        var lifted = await admin.PostAsJsonAsync(lift, Request(Guid.NewGuid(), null));
        Assert.Equal(HttpStatusCode.NoContent, lifted.StatusCode);
        Assert.True(lifted.Headers.CacheControl?.NoStore);
        var unchanged = await admin.PostAsJsonAsync(lift, Request(Guid.NewGuid(), null));
        Assert.Equal(HttpStatusCode.Conflict, unchanged.StatusCode);
        Assert.True(unchanged.Headers.CacheControl?.NoStore);
        var history = (await admin.GetFromJsonAsync<AccountEventPage>($"/api/staff-accounts/{customerId}/history"))!;
        Assert.Equal(["SuspensionLifted", "Suspended"], history.Items.Select(item => item.Kind));
        var suspended = history.Items[1];
        Assert.Equal(("Amir Hakim", "Chargeback dispute"), (suspended.Actor, suspended.Reason));
        Assert.Contains(suspended.Changes, change => change is { Field: "Password", After: "Reset required" });
        Assert.Equal(HttpStatusCode.Unauthorized, (await customer.GetAsync($"/api/staff-accounts/{customerId}/history")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/staff-accounts/{Guid.NewGuid()}/history")).StatusCode);
        // The password was replaced, so the recovery flow the lift re-opened is the way back in.
        await visitor.PostAsJsonAsync("/api/auth/password-recovery", new { email = registration.Email });
        Assert.True(await HasRecoveryChallengeAsync(factory, registration.Email));
    }

    [Fact]
    public async Task AdministratorsCannotSuspendThemselvesOrBeSuspendedByAnotherRole()
    {
        using var factory = fixture.CreateFactory();
        using var admin = factory.CreateClient();
        await StaffInvitationTests.SignInAdministratorAsync(factory, admin);
        var adminId = (await admin.GetFromJsonAsync<AccountProfile>("/api/account/profile"))!.UserId;

        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync($"/api/accounts/{adminId}/suspensions",
            Request(Guid.NewGuid(), "Testing"))).StatusCode);
        var missing = await admin.PostAsJsonAsync($"/api/accounts/{Guid.NewGuid()}/suspensions",
            Request(Guid.NewGuid(), "Testing"));
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.True(missing.Headers.CacheControl?.NoStore);
        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync($"/api/accounts/{adminId}/suspensions",
            Request(Guid.NewGuid(), "Testing"))).StatusCode);
    }

    private static object Request(Guid operationId, string? reason, bool reset = false, bool verify = false) =>
        new { operationId, reason, requirePasswordReset = reset, requireEmailVerification = verify };

    private static async Task<Guid> CustomerIdAsync(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory, string email)
    {
        using var scope = factory.Services.CreateScope();
        var normalized = email.ToUpperInvariant();
        return await scope.ServiceProvider.GetRequiredService<DealershipDbContext>().Users
            .Where(user => user.NormalizedEmail == normalized).Select(user => user.Id).SingleAsync();
    }

    private static async Task<int> EventCountAsync(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory,
        Guid targetId, AccountEventKind kind)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<DealershipDbContext>().AccountEvents
            .CountAsync(item => item.TargetId == targetId && item.Kind == kind);
    }

    private static async Task<bool> HasRecoveryChallengeAsync(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory,
        string email)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<DealershipDbContext>().PasswordResetChallenges
            .AnyAsync(challenge => challenge.RecipientEmail == email);
    }
}

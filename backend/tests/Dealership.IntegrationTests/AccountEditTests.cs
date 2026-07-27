using System.Net;
using System.Net.Http.Json;
using Dealership.Application.Accounts;
using Dealership.Domain.Accounts;
using Dealership.Infrastructure.Identity;
using Dealership.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dealership.IntegrationTests;

public sealed class AccountEditTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task EditingACustomerNeedsAReasonReplaysByOperationAndLogsTheChanges()
    {
        using var factory = fixture.CreateFactory();
        using var admin = factory.CreateClient();
        await StaffInvitationTests.SignInAdministratorAsync(factory, admin);
        using var customer = factory.CreateClient();
        var registration = await IntakePhotoFixture.RegisterAsync(customer);
        var id = await UserIdAsync(factory, registration.Email!);
        var path = $"/api/accounts/{id}/edits";

        var invalid = await admin.PostAsJsonAsync(path,
            Edit(Guid.NewGuid(), " ", registration.Email!, address: "1 Jalan Baru"));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.True(invalid.Headers.CacheControl?.NoStore);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync(path,
            Edit(Guid.NewGuid(), "Typo fix", "not-an-email", address: "1 Jalan Baru"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync(path,
            Edit(Guid.NewGuid(), "Typo fix", registration.Email!))).StatusCode);
        var denied = await customer.PostAsJsonAsync(path,
            Edit(Guid.NewGuid(), "Self edit", registration.Email!, address: "1 Jalan Baru"));
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.True(denied.Headers.CacheControl?.NoStore);
        var unchanged = await admin.PostAsJsonAsync(path,
            Edit(Guid.NewGuid(), "Nothing changes", registration.Email!, name: registration.LegalName!,
                mobile: registration.Mobile!, address: registration.ResidentialAddress));
        Assert.Equal(HttpStatusCode.Conflict, unchanged.StatusCode);
        Assert.True(unchanged.Headers.CacheControl?.NoStore);

        var operation = Guid.NewGuid();
        var edit = Edit(operation, "Customer moved house", registration.Email!, name: "Nur Aisyah", mobile: "0127654321",
            address: "9 Jalan Cempaka, Shah Alam");
        var edited = await admin.PostAsJsonAsync(path, edit);
        Assert.Equal(HttpStatusCode.NoContent, edited.StatusCode);
        Assert.True(edited.Headers.CacheControl?.NoStore);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsJsonAsync(path, edit)).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await customer.GetAsync("/api/account/profile")).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var profile = await scope.ServiceProvider.GetRequiredService<DealershipDbContext>().CustomerProfiles
                .SingleAsync(item => item.UserId == id);
            Assert.Equal(("Nur Aisyah", "0127654321", "9 Jalan Cempaka, Shah Alam"),
                (profile.LegalName, profile.Mobile, profile.ResidentialAddress));
        }

        var history = (await admin.GetFromJsonAsync<AccountEventPage>($"/api/staff-accounts/{id}/history"))!;
        var recorded = Assert.Single(history.Items);
        Assert.Equal(("ProfileEdited", "Customer moved house"), (recorded.Kind, recorded.Reason));
        Assert.Equal(["Name", "Mobile", "Address"], recorded.Changes.Select(change => change.Field));
    }

    [Fact]
    public async Task ChangingAStaffEmailAppliesAtOnceEndsSessionsAndResetLinksAndAsksForVerification()
    {
        using var factory = fixture.CreateFactory();
        using var admin = factory.CreateClient();
        var branchId = await StaffInvitationTests.SignInAdministratorAsync(factory, admin);
        var staff = await ProvisionAgentAsync(factory, branchId);
        using var agent = factory.CreateClient();
        await SignInAsync(agent, staff.Email, "Password123!");
        using var visitor = factory.CreateClient();
        await visitor.PostAsJsonAsync("/api/auth/password-recovery", new { email = staff.Email });
        Assert.Equal(1, await UnusedResetLinksAsync(factory, staff.Id));
        var path = $"/api/accounts/{staff.Id}/edits";
        var newEmail = $"agent.moved.{Guid.NewGuid():N}@example.invalid";

        var taken = (await admin.GetFromJsonAsync<AccountProfile>("/api/account/profile"))!.Email;
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync(path,
            Edit(Guid.NewGuid(), "Taken address", taken, name: "Lim Aina", mobile: "0121234567"))).StatusCode);

        var operation = Guid.NewGuid();
        var edit = Edit(operation, "Moved to the new mailbox", newEmail, name: "Lim Aina Binti Ali", mobile: "0121234567",
            verify: true);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsJsonAsync(path, edit)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsJsonAsync(path, edit)).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await agent.GetAsync("/api/account/profile")).StatusCode);
        Assert.Equal(0, await UnusedResetLinksAsync(factory, staff.Id));
        using var fresh = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await fresh.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(Guid.NewGuid(), staff.Email, "Password123!"))).StatusCode);
        await SignInAsync(fresh, newEmail, "Password123!");

        using (var scope = factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
            var user = await database.Users.SingleAsync(item => item.Id == staff.Id);
            Assert.False(user.EmailConfirmed);
            Assert.Equal(1, await database.EmailVerificationChallenges.CountAsync(challenge =>
                challenge.UserId == staff.Id && challenge.RecipientEmail == newEmail && challenge.InvalidatedAt == null));
            Assert.Equal("Lim Aina Binti Ali", (await database.StaffProfiles.SingleAsync(item => item.UserId == staff.Id)).Name);
        }

        var history = (await admin.GetFromJsonAsync<AccountEventPage>($"/api/staff-accounts/{staff.Id}/history"))!;
        var recorded = Assert.Single(history.Items);
        Assert.Equal("EmailChanged", recorded.Kind);
        Assert.Contains(recorded.Changes, change => change.Field == "Email" && change.After == newEmail);
        Assert.Contains(recorded.Changes, change => change.Field == "Email verification");
        Assert.DoesNotContain(recorded.Changes, change => change.Field == "Password");
    }

    [Fact]
    public async Task ARequiredPasswordResetReplacesThePasswordAndAdministratorsCannotEditThemselves()
    {
        using var factory = fixture.CreateFactory();
        using var admin = factory.CreateClient();
        var branchId = await StaffInvitationTests.SignInAdministratorAsync(factory, admin);
        var staff = await ProvisionAgentAsync(factory, branchId);
        var newEmail = $"agent.reset.{Guid.NewGuid():N}@example.invalid";

        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsJsonAsync($"/api/accounts/{staff.Id}/edits",
            Edit(Guid.NewGuid(), "Suspected takeover", newEmail, name: "Lim Aina", mobile: "0121234567", reset: true))).StatusCode);
        using var fresh = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await fresh.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(Guid.NewGuid(), newEmail, "Password123!"))).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
            Assert.True((await database.Users.SingleAsync(item => item.Id == staff.Id)).EmailConfirmed);
        }

        var adminProfile = (await admin.GetFromJsonAsync<AccountProfile>("/api/account/profile"))!;
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync($"/api/accounts/{adminProfile.UserId}/edits",
            Edit(Guid.NewGuid(), "Self edit", adminProfile.Email, name: "Someone Else", mobile: "0123456789"))).StatusCode);
        var missing = await admin.PostAsJsonAsync($"/api/accounts/{Guid.NewGuid()}/edits",
            Edit(Guid.NewGuid(), "Nobody", "nobody@example.invalid", name: "Nobody", mobile: "0123456789"));
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.True(missing.Headers.CacheControl?.NoStore);
        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync($"/api/accounts/{staff.Id}/edits",
            Edit(Guid.NewGuid(), "Anonymous", newEmail, name: "Lim Aina", mobile: "0121234567"))).StatusCode);
    }

    private static object Edit(Guid operationId, string? reason, string email, string name = "Name", string mobile = "0120000000",
        string? address = null, bool reset = false, bool verify = false) => new
        {
            operationId,
            reason,
            name,
            mobile,
            address,
            email,
            requirePasswordReset = reset,
            requireEmailVerification = verify
        };

    private sealed record ProvisionedStaff(Guid Id, string Email);

    private static async Task<ProvisionedStaff> ProvisionAgentAsync(WebApplicationFactory<Program> factory, Guid branchId)
    {
        using var scope = factory.Services.CreateScope();
        var id = Guid.NewGuid();
        var email = $"agent.{Guid.NewGuid():N}@example.invalid";
        Assert.True((await scope.ServiceProvider.GetRequiredService<StaffProvisioningService>().ProvisionStaffAsync(
            new(id, email, "Password123!", AccountRoles.SalesAgent, branchId, "Lim Aina", "0121234567"),
            CancellationToken.None)).Created);
        return new ProvisionedStaff(id, email);
    }

    private static async Task SignInAsync(HttpClient client, string email, string password)
    {
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(Guid.NewGuid(), email, password));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var session = (await login.Content.ReadFromJsonAsync<LoginSession>())!;
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", session.AccessToken);
    }

    private static async Task<Guid> UserIdAsync(WebApplicationFactory<Program> factory, string email)
    {
        using var scope = factory.Services.CreateScope();
        var normalized = email.ToUpperInvariant();
        return await scope.ServiceProvider.GetRequiredService<DealershipDbContext>().Users
            .Where(user => user.NormalizedEmail == normalized).Select(user => user.Id).SingleAsync();
    }

    private static async Task<int> UnusedResetLinksAsync(WebApplicationFactory<Program> factory, Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<DealershipDbContext>().PasswordResetChallenges
            .CountAsync(challenge => challenge.UserId == userId && challenge.InvalidatedAt == null &&
                challenge.ConsumedAt == null);
    }
}

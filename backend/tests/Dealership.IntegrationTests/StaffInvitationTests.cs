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

namespace Dealership.IntegrationTests;

public sealed class StaffInvitationTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task InvitationsReplayRenewRevokeAndActivateOnceIntoAVerifiedAccount()
    {
        using var factory = fixture.CreateFactory();
        using var admin = factory.CreateClient();
        var branchId = await SignInAdministratorAsync(factory, admin);
        var invitationId = Guid.NewGuid();
        var email = $"new.agent.{Guid.NewGuid():N}@example.invalid";
        var body = Body(invitationId, email, branchId);

        Assert.Equal(HttpStatusCode.Accepted, (await admin.PostAsJsonAsync("/api/staff-invitations", body)).StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, (await admin.PostAsJsonAsync("/api/staff-invitations", body)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/staff-invitations",
            Body(invitationId, email, branchId, name: "Someone Else"))).StatusCode);
        var clash = await admin.PostAsJsonAsync("/api/staff-invitations", Body(Guid.NewGuid(), email.ToUpperInvariant(), branchId));
        Assert.Equal(HttpStatusCode.Conflict, clash.StatusCode);
        Assert.False(await AccountExistsAsync(factory, email));

        using var visitor = factory.CreateClient();
        var firstToken = await TokenAsync(factory, invitationId);
        var preview = await visitor.PostAsJsonAsync("/api/auth/staff-invitation/preview", new { token = firstToken });
        var shown = (await preview.Content.ReadFromJsonAsync<PublicStaffInvitation>())!;
        Assert.Equal(("Aina Sofia", AccountRoles.SalesAgent), (shown.Name, shown.Role));
        Assert.Equal(HttpStatusCode.NotFound, (await visitor.PostAsJsonAsync("/api/auth/staff-invitation/preview",
            new { token = firstToken[..^1] + (firstToken[^1] == '0' ? '1' : '0') })).StatusCode);

        Assert.Equal(HttpStatusCode.TooManyRequests, (await admin.PostAsync($"/api/staff-invitations/{invitationId}/resends", null)).StatusCode);
        await ClearSendHistoryAsync(factory, invitationId);
        Assert.Equal(HttpStatusCode.Accepted, (await admin.PostAsync($"/api/staff-invitations/{invitationId}/resends", null)).StatusCode);
        var secondToken = await TokenAsync(factory, invitationId);
        Assert.NotEqual(firstToken, secondToken);
        Assert.Equal(HttpStatusCode.NotFound, (await visitor.PostAsJsonAsync("/api/auth/staff-invitation/preview", new { token = firstToken })).StatusCode);

        await ClearSendHistoryAsync(factory, invitationId);
        var corrected = $"corrected.{Guid.NewGuid():N}@example.invalid";
        Assert.Equal(HttpStatusCode.Accepted, (await admin.PutAsJsonAsync($"/api/staff-invitations/{invitationId}/email", new { email = corrected })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await visitor.PostAsJsonAsync("/api/auth/staff-invitation/preview", new { token = secondToken })).StatusCode);
        var thirdToken = await TokenAsync(factory, invitationId);

        Assert.Equal(HttpStatusCode.BadRequest, (await Activate(visitor, thirdToken, "short", "aina.sofia")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Activate(visitor, thirdToken, "Password123!", "no")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Activate(visitor, thirdToken, "Password123!", "aina.sofia")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Activate(visitor, thirdToken, "Password123!", "aina.sofia")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Activate(visitor, thirdToken, "Another123!", "aina.sofia")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await visitor.PostAsJsonAsync("/api/auth/staff-invitation/preview", new { token = thirdToken })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsync($"/api/staff-invitations/{invitationId}/revocations", null)).StatusCode);

        var login = await visitor.PostAsJsonAsync("/api/auth/login", new LoginRequest(Guid.NewGuid(), corrected, "Password123!"));
        var session = (await login.Content.ReadFromJsonAsync<LoginSession>())!;
        visitor.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        var profile = (await visitor.GetFromJsonAsync<AccountProfile>("/api/account/profile"))!;
        Assert.Equal((AccountRoles.SalesAgent, "aina.sofia", true, branchId, "Aina Sofia"),
            (profile.Role, profile.Username, profile.EmailVerified, profile.BranchId, profile.Name));
    }

    [Fact]
    public async Task RevokingReleasesTheEmailAndBlocksActivationWhileOnlyAdministratorsInvite()
    {
        using var factory = fixture.CreateFactory();
        using var admin = factory.CreateClient();
        var branchId = await SignInAdministratorAsync(factory, admin);
        var invitationId = Guid.NewGuid();
        var email = $"revoked.{Guid.NewGuid():N}@example.invalid";
        Assert.Equal(HttpStatusCode.Accepted, (await admin.PostAsJsonAsync("/api/staff-invitations", Body(invitationId, email, branchId))).StatusCode);
        var token = await TokenAsync(factory, invitationId);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"/api/staff-invitations/{invitationId}/revocations", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"/api/staff-invitations/{invitationId}/revocations", null)).StatusCode);
        using var visitor = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Conflict, (await Activate(visitor, token, "Password123!", "revoked.user")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsync($"/api/staff-invitations/{invitationId}/resends", null)).StatusCode);
        Assert.False(await AccountExistsAsync(factory, email));
        Assert.Equal(HttpStatusCode.Accepted, (await admin.PostAsJsonAsync("/api/staff-invitations", Body(Guid.NewGuid(), email, branchId))).StatusCode);

        using var customer = factory.CreateClient();
        await IntakePhotoFixture.RegisterAsync(customer);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.PostAsJsonAsync("/api/staff-invitations",
            Body(Guid.NewGuid(), $"other.{Guid.NewGuid():N}@example.invalid", branchId))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/staff-invitations",
            new
            {
                id = Guid.NewGuid(),
                email = $"admin.branch.{Guid.NewGuid():N}@example.invalid",
                role = AccountRoles.Administrator,
                branchId,
                name = "Aina Sofia",
                mobile = "0129876543"
            })).StatusCode);
    }

    private static object Body(Guid id, string email, Guid branchId, string role = AccountRoles.SalesAgent,
        string name = "Aina Sofia")
    {
        return new { id, email, role, branchId = role == AccountRoles.SalesAgent ? (Guid?)branchId : null, name, mobile = "0129876543" };
    }

    private static Task<HttpResponseMessage> Activate(HttpClient client, string token, string password, string username)
    {
        return client.PostAsJsonAsync("/api/auth/staff-invitation/activation", new { token, password, username });
    }

    internal static async Task<Guid> SignInAdministratorAsync(WebApplicationFactory<Program> factory, HttpClient client)
    {
        using var scope = factory.Services.CreateScope();
        var branchId = Guid.NewGuid();
        await scope.ServiceProvider.GetRequiredService<BranchProvisioningService>().ProvisionBranchAsync(
            new(branchId, $"ApexAuto {branchId:N}", "12 Jalan Pengaturcara", "Shah Alam", "Selangor", "+60355651234"),
            CancellationToken.None);
        var email = $"admin.{Guid.NewGuid():N}@example.invalid";
        Assert.True((await scope.ServiceProvider.GetRequiredService<StaffProvisioningService>().ProvisionStaffAsync(
            new(Guid.NewGuid(), email, "Password123!", AccountRoles.Administrator, null, "Amir Hakim", "0123456789"),
            CancellationToken.None)).Created);
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(Guid.NewGuid(), email, "Password123!"));
        var session = (await login.Content.ReadFromJsonAsync<LoginSession>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        return branchId;
    }

    private static async Task<string> TokenAsync(WebApplicationFactory<Program> factory, Guid invitationId)
    {
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        var invitation = await database.StaffInvitations.AsNoTracking().SingleAsync(item => item.Id == invitationId);
        return factory.Services.GetRequiredService<StaffInvitationToken>().Create(invitation);
    }

    private static async Task<bool> AccountExistsAsync(WebApplicationFactory<Program> factory, string email)
    {
        using var scope = factory.Services.CreateScope();
        var normalized = email.ToUpperInvariant();
        return await scope.ServiceProvider.GetRequiredService<DealershipDbContext>().Users
            .AnyAsync(user => user.NormalizedEmail == normalized);
    }

    // The test clock is real time; moving the recorded sends back stands in for waiting out the limit.
    private static async Task ClearSendHistoryAsync(WebApplicationFactory<Program> factory, Guid invitationId)
    {
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<DealershipDbContext>().Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"StaffInvitations\" SET \"SentAt\" = ARRAY[now() - interval '3 hours']::timestamptz[] WHERE \"Id\" = {invitationId}");
    }
}

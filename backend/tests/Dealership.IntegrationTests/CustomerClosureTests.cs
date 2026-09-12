using System.Net;
using System.Net.Http.Json;
using Dealership.Application.Accounts;
using Dealership.Domain.Accounts;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dealership.IntegrationTests;

public sealed class CustomerClosureTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task ClosureNeedsThePasswordRemovesIdentityAndKeepsReplayAndReuseSafe()
    {
        using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var registration = await IntakePhotoFixture.RegisterAsync(client);
        var operationId = Guid.NewGuid();

        var wrong = await client.PostAsJsonAsync("/api/account/closure", new { operationId, password = "Wrong-Password1!" });
        Assert.True(wrong.StatusCode == HttpStatusCode.Conflict, await wrong.Content.ReadAsStringAsync());
        Assert.Contains("wrong-password", await wrong.Content.ReadAsStringAsync());

        var closed = await client.PostAsJsonAsync("/api/account/closure", new { operationId, password = registration.Password });
        Assert.Equal(HttpStatusCode.OK, closed.StatusCode);
        var receipt = (await closed.Content.ReadFromJsonAsync<CustomerClosureReceipt>())!;
        Assert.Equal(operationId, receipt.ReceiptId);
        Assert.NotEmpty(receipt.Retained);

        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        var user = await database.Users.AsNoTracking().SingleAsync(item =>
            database.AccountEvents.Any(entry => entry.Id == operationId && entry.TargetId == item.Id));
        Assert.NotNull(user.ClosedAt);
        Assert.False(user.IsActive);
        Assert.Null(user.Email);
        Assert.Null(user.PasswordHash);
        Assert.Null(user.AccountUsername);
        Assert.NotEmpty(user.SecurityStamp!);
        var profile = await database.CustomerProfiles.AsNoTracking().SingleAsync(item => item.UserId == user.Id);
        Assert.NotNull(profile.ErasedAt);
        Assert.Null(profile.Nric);
        Assert.Null(await database.EmailVerificationChallenges.Where(item => item.UserId == user.Id)
            .Select(item => item.RecipientEmail).FirstOrDefaultAsync());

        using var stranger = factory.CreateClient();
        var login = await stranger.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(Guid.NewGuid(), registration.Email, registration.Password));
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/account/closure/blockers")).StatusCode);

        // The former email and NRIC are free: a new registration gets a new identity.
        var again = registration with { RegistrationKey = Guid.NewGuid(), Username = TestUsernames.Next() };
        Assert.Equal(HttpStatusCode.NoContent, (await stranger.PostAsJsonAsync("/api/auth/register", again)).StatusCode);
        Assert.Equal(2, await database.Users.CountAsync(item => item.Role == AccountRoles.Customer &&
            database.CustomerProfiles.Any(entry => entry.UserId == item.Id && entry.RegistrationKey != Guid.Empty)));
    }
}

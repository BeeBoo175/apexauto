using System.Net;
using System.Net.Http.Json;
using Dealership.Application.Accounts;
using Dealership.Domain.Accounts;
using Dealership.Infrastructure.Identity;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dealership.IntegrationTests;

public sealed class AdministratorStaffErasureTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    private const string Password = "Password123!";

    [Fact]
    public async Task AnAdministratorErasesAnEmployeeButNeverThemselves()
    {
        using var factory = fixture.CreateFactory();
        using var admin = factory.CreateClient();
        var branchId = await StaffInvitationTests.SignInAdministratorAsync(factory, admin);
        var agentId = Guid.NewGuid();
        var email = $"agent.{Guid.NewGuid():N}@example.invalid";
        using (var scope = factory.Services.CreateScope())
        {
            Assert.True((await scope.ServiceProvider.GetRequiredService<StaffProvisioningService>().ProvisionStaffAsync(
                new(agentId, email, Password, AccountRoles.SalesAgent, branchId, "Hana Ismail", "0123456781"),
                CancellationToken.None)).Created);
        }

        var userId = await UserIdAsync(factory, email);
        var selfId = await UserIdAsync(factory, await AdministratorEmailAsync(factory));
        var operationId = Guid.NewGuid();

        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/staff-accounts/{userId}/blockers")).StatusCode);
        var self = await admin.PostAsJsonAsync($"/api/staff-accounts/{selfId}/erasures",
            new { operationId = Guid.NewGuid(), reason = "Test", password = Password });
        Assert.Equal(HttpStatusCode.Conflict, self.StatusCode);
        Assert.Contains("protected", await self.Content.ReadAsStringAsync());

        var erased = await admin.PostAsJsonAsync($"/api/staff-accounts/{userId}/erasures",
            new { operationId, reason = "Left the company", password = Password });
        Assert.Equal(HttpStatusCode.OK, erased.StatusCode);
        var receipt = (await erased.Content.ReadFromJsonAsync<CustomerClosureReceipt>())!;
        Assert.Contains(receipt.Retained, item => item.Name == "Work history");
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/api/staff-accounts/{userId}/erasures",
            new { operationId, reason = "Left the company", password = Password })).StatusCode);

        using var verify = factory.Services.CreateScope();
        var database = verify.ServiceProvider.GetRequiredService<DealershipDbContext>();
        var user = await database.Users.AsNoTracking().SingleAsync(item => item.Id == userId);
        Assert.NotNull(user.ClosedAt);
        Assert.Null(user.Email);
        Assert.Null(user.PasswordHash);
        var profile = await database.StaffProfiles.AsNoTracking().SingleAsync(item => item.UserId == userId);
        Assert.NotNull(profile.ErasedAt);
        Assert.Null(profile.Name);
        Assert.Null(profile.Mobile);
        var directory = await admin.GetAsync("/api/staff-accounts?pageSize=50");
        Assert.Equal(HttpStatusCode.OK, directory.StatusCode);
        Assert.Contains("Former employee", await directory.Content.ReadAsStringAsync());
    }

    private static async Task<Guid> UserIdAsync(
        Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory, string email)
    {
        using var scope = factory.Services.CreateScope();
        var normalized = email.ToUpperInvariant();
        return await scope.ServiceProvider.GetRequiredService<DealershipDbContext>().Users
            .Where(user => user.NormalizedEmail == normalized).Select(user => user.Id).SingleAsync();
    }

    private static async Task<string> AdministratorEmailAsync(
        Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory)
    {
        using var scope = factory.Services.CreateScope();
        return (await scope.ServiceProvider.GetRequiredService<DealershipDbContext>().Users
            .Where(user => user.Role == AccountRoles.Administrator).Select(user => user.Email).SingleAsync())!;
    }
}

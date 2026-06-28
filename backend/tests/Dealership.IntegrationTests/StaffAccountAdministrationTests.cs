using System.Net;
using System.Net.Http.Json;
using Dealership.Application.Accounts;
using Dealership.Application.Branches;
using Dealership.Domain.Accounts;
using Dealership.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dealership.IntegrationTests;

public sealed class StaffAccountAdministrationTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task AdministratorsTransferDeactivateAndReactivateStaffWithReasonsReplayAndProtection()
    {
        using var factory = fixture.CreateFactory();
        using var admin = factory.CreateClient();
        var branchA = await StaffInvitationTests.SignInAdministratorAsync(factory, admin);
        var branchB = await ProvisionBranchAsync(factory);
        var staffId = await ProvisionAgentAsync(factory, branchA);
        var adminId = (await admin.GetFromJsonAsync<AccountProfile>("/api/account/profile"))!.UserId;
        var path = $"/api/staff-accounts/{staffId}";

        var blockers = await admin.GetFromJsonAsync<BlockersBody>($"{path}/blockers");
        Assert.Empty(blockers!.Blockers);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/staff-accounts/{Guid.NewGuid()}/blockers")).StatusCode);

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"{path}/transfers",
            new { operationId = Guid.NewGuid(), reason = (string?)null, branchId = branchB })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync($"{path}/transfers",
            new { operationId = Guid.NewGuid(), reason = "Same place", branchId = branchA })).StatusCode);
        var transfer = Guid.NewGuid();
        var transferBody = new { operationId = transfer, reason = "Covering Bayan Lepas", branchId = branchB };
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsJsonAsync($"{path}/transfers", transferBody)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsJsonAsync($"{path}/transfers", transferBody)).StatusCode);
        Assert.Equal(branchB, await BranchOfAsync(factory, staffId));

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"{path}/deactivations",
            new { operationId = Guid.NewGuid(), reason = " " })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync($"/api/staff-accounts/{adminId}/deactivations",
            new { operationId = Guid.NewGuid(), reason = "Self" })).StatusCode);
        var deactivation = new { operationId = Guid.NewGuid(), reason = "Left the company" };
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsJsonAsync($"{path}/deactivations", deactivation)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsJsonAsync($"{path}/deactivations", deactivation)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync($"{path}/deactivations",
            new { operationId = Guid.NewGuid(), reason = "Again" })).StatusCode);
        var deactivated = (await admin.GetFromJsonAsync<StaffAccountPage>("/api/staff-accounts?tab=Deactivated"))!;
        Assert.Contains(deactivated.Items, row => row.Id == staffId);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsJsonAsync($"{path}/reactivations",
            new { operationId = Guid.NewGuid() })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync($"{path}/reactivations",
            new { operationId = Guid.NewGuid() })).StatusCode);

        var history = (await admin.GetFromJsonAsync<AccountEventPage>($"{path}/history"))!;
        Assert.Equal(["Reactivated", "Deactivated", "BranchTransferred"], history.Items.Select(item => item.Kind));
        Assert.Contains(history.Items[2].Changes, change => change.Field == "Branch" && change.After is not null);

        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync($"{path}/deactivations", deactivation)).StatusCode);
    }

    private sealed record BlockersBody(List<AccountBlocker> Blockers);

    private static async Task<Guid> ProvisionBranchAsync(WebApplicationFactory<Program> factory)
    {
        using var scope = factory.Services.CreateScope();
        var branchId = Guid.NewGuid();
        await scope.ServiceProvider.GetRequiredService<BranchProvisioningService>().ProvisionBranchAsync(
            new(branchId, $"ApexAuto {branchId:N}", "12 Jalan Pengaturcara", "Shah Alam", "Selangor", "+60355651234"),
            CancellationToken.None);
        return branchId;
    }

    private static async Task<Guid> ProvisionAgentAsync(WebApplicationFactory<Program> factory, Guid branchId)
    {
        using var scope = factory.Services.CreateScope();
        var id = Guid.NewGuid();
        Assert.True((await scope.ServiceProvider.GetRequiredService<StaffProvisioningService>().ProvisionStaffAsync(
            new(id, $"agent.{Guid.NewGuid():N}@example.invalid", "Password123!", AccountRoles.SalesAgent, branchId,
                "Lim Aina", "0121234567"), CancellationToken.None)).Created);
        return id;
    }

    private static async Task<Guid?> BranchOfAsync(WebApplicationFactory<Program> factory, Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<DealershipDbContext>().Users.AsNoTracking()
            .Where(user => user.Id == userId).Select(user => user.BranchId).SingleAsync();
    }
}

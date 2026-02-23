using System.Data;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Dealership.Application.Accounts;
using Dealership.Application.Branches;
using Dealership.Application.Inspections;
using Dealership.Application.Intake;
using Dealership.Domain.Inspections;
using Dealership.Infrastructure.Development;
using Dealership.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Dealership.IntegrationTests;

public sealed class InspectionRepairApprovalTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task ApprovalsRetainExactManagerAuthorityAcrossRenewalReopeningAndReconstruction()
    {
        using var data = await CheckInTestData.CreateAsync(fixture);
        var appointment = await data.BookAsync();
        using var scope = data.Factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var checkIn = await services.GetRequiredService<AppointmentCheckInService>()
            .CheckInAsync(data.StaffId, appointment.Id, CheckInTestData.Evidence, CancellationToken.None);
        var (technician, _) = await ProvisionAsync(services, data.BranchId, AccountRoles.WorkshopTechnician);
        var (manager, email) = await ProvisionAsync(services, data.BranchId, AccountRoles.BranchManager);
        var (otherManager, _) = await ProvisionAsync(services, data.BranchId, AccountRoles.BranchManager);
        var (group, _) = await ProvisionAsync(services, null, AccountRoles.GeneralManager);
        var (admin, _) = await ProvisionAsync(services, null, AccountRoles.Administrator);
        var (colleague, _) = await ProvisionAsync(services, data.BranchId, AccountRoles.WorkshopTechnician);
        var branch = Guid.NewGuid();
        await services.GetRequiredService<BranchProvisioningService>().ProvisionBranchAsync(
            DevelopmentFixtureManifest.Branches[1] with { Id = branch }, CancellationToken.None);
        var (outside, _) = await ProvisionAsync(services, branch, AccountRoles.BranchManager);
        var claim = await services.GetRequiredService<InspectionAssignmentService>().AssignAsync(technician,
            checkIn.Receipt!.VehicleId, new(Guid.NewGuid(), technician, 0, null), CancellationToken.None);
        var inspection = claim.Receipt!.InspectionId;
        var approval = new InspectionRepairApprovalRequest(Guid.NewGuid(), Guid.NewGuid(), 1, 1, 1);
        Assert.Equal(InspectionAssignmentStatus.Conflict, (await ApproveAsync(data.Factory, manager, inspection, approval)).Status);
        var save = new InspectionDraftSaveRequest(Guid.NewGuid(), 1, 0,
            [new("EP-004", InspectionEntryKind.Finding, InspectionSeverity.MinorDefect, 250m, "Scratched front bumper")])
        {
            ExaminedPointCodes = [.. InspectionCatalog.Points.Select(point => point.Code)]
        };
        await services.GetRequiredService<InspectionDraftSaveService>().SaveAsync(technician, inspection, save, CancellationToken.None);
        var reports = services.GetRequiredService<IInspectionReportStore>();
        var signoffs = services.GetRequiredService<InspectionSignoffService>();
        var original = (await signoffs.SignAsync(technician, inspection, new(Guid.NewGuid(), 1, 1, 1), CancellationToken.None)).Report!;
        var request = new InspectionRepairRequest(Guid.NewGuid(), 1, 1, 0, [new("EP-004", "Refinish bumper")], 250m);
        var repairs = services.GetRequiredService<IInspectionRepairRequestStore>();
        var requested = (await repairs.RequestAsync(technician, inspection, request, CancellationToken.None)).Receipt!;
        approval = approval with { RequestId = requested.OperationId };
        foreach (var actor in new[] { technician, colleague, group, admin, data.StaffId, data.OwnerId })
        {
            Assert.Equal(InspectionAssignmentStatus.Forbidden, (await ApproveAsync(data.Factory, actor, inspection, approval)).Status);
        }
        Assert.Equal(InspectionAssignmentStatus.NotFound, (await ApproveAsync(data.Factory, outside, inspection, approval)).Status);
        await InspectionRepairApprovalSqlAssertions.VerifyBeforeAsync(fixture.Database, requested, manager, technician, group, outside);
        var race = await Task.WhenAll(ApproveAsync(data.Factory, manager, inspection, approval),
            ApproveAsync(data.Factory, manager, inspection, approval with { OperationId = Guid.NewGuid() }));
        var receipt = Assert.Single(race, result => result.Status == InspectionAssignmentStatus.Succeeded).Receipt!;
        Assert.Single(race, result => result.Status == InspectionAssignmentStatus.Conflict);
        approval = approval with { OperationId = receipt.OperationId };
        await InspectionRepairApprovalSqlAssertions.VerifyAfterAsync(fixture.Database, receipt);

        using var restarted = fixture.CreateFactory();
        var replay = await Task.WhenAll(ApproveAsync(restarted, manager, inspection, approval), ApproveAsync(restarted, manager, inspection, approval));
        Assert.All(replay, result => Assert.Equal(JsonSerializer.Serialize(receipt), JsonSerializer.Serialize(result.Receipt)));
        Assert.Equal(InspectionAssignmentStatus.Conflict, (await ApproveAsync(restarted, otherManager, inspection, approval)).Status);
        Assert.Equal(InspectionAssignmentStatus.Conflict,
            (await ApproveAsync(restarted, manager, inspection, approval with { RepairVersion = 2 })).Status);
        var approvals = services.GetRequiredService<IInspectionRepairApprovalStore>();
        foreach (var actor in new[] { technician, manager, group })
        {
            Assert.Equal(receipt, (await approvals.ReadAsync(actor, inspection, 1, CancellationToken.None)).Receipt);
        }
        Assert.Equal(InspectionAssignmentStatus.NotFound, (await approvals.ReadAsync(outside, inspection, 1, CancellationToken.None)).Status);
        Assert.Equal(InspectionAssignmentStatus.NotFound, (await approvals.ReadAsync(colleague, inspection, 1, CancellationToken.None)).Status);
        Assert.Equal(InspectionAssignmentStatus.Forbidden, (await approvals.ReadAsync(admin, inspection, 1, CancellationToken.None)).Status);
        await VerifyApiAsync(restarted, email, inspection, approval, receipt);

        // A changed scope needs its own authority even when its budget is lower.
        await using var stale = new NpgsqlConnection(fixture.Database.RuntimeConnectionString);
        await stale.OpenAsync();
        await using var oldSnapshot = await stale.BeginTransactionAsync(IsolationLevel.RepeatableRead);
        await using (var read = new NpgsqlCommand("SELECT \"RepairVersion\" FROM \"InspectionDrafts\" WHERE \"Id\" = @inspection", stale, oldSnapshot))
        {
            read.Parameters.AddWithValue("inspection", inspection);
            Assert.Equal(1L, await read.ExecuteScalarAsync());
        }
        request = request with { OperationId = Guid.NewGuid(), ExpectedVersion = 1, Budget = 200m, Items = [new("EP-004", "Polish bumper surface")] };
        requested = (await repairs.RequestAsync(technician, inspection, request, CancellationToken.None)).Receipt!;
        await InspectionRepairApprovalSqlAssertions.VerifyStaleSnapshotAsync(stale, oldSnapshot, receipt with { OperationId = Guid.NewGuid() });
        await oldSnapshot.RollbackAsync();
        Assert.Equal(InspectionAssignmentStatus.Conflict,
            (await ApproveAsync(restarted, manager, inspection, approval with { OperationId = Guid.NewGuid() })).Status);
        var renewed = new InspectionRepairApprovalRequest(Guid.NewGuid(), requested.OperationId, 1, 1, 2);
        Assert.Equal(InspectionAssignmentStatus.Succeeded, (await ApproveAsync(restarted, manager, inspection, renewed)).Status);
        request = request with { OperationId = Guid.NewGuid(), ExpectedVersion = 2, Budget = 300m };
        requested = (await repairs.RequestAsync(technician, inspection, request, CancellationToken.None)).Receipt!;
        renewed = renewed with { OperationId = Guid.NewGuid(), RequestId = requested.OperationId, RepairVersion = 3 };
        Assert.Equal(InspectionAssignmentStatus.Succeeded, (await ApproveAsync(restarted, manager, inspection, renewed)).Status);
        Assert.Equal(3, (await approvals.ReadAsync(group, inspection, 1, CancellationToken.None)).Receipt!.RepairVersion);
        Assert.Equal(receipt, (await ApproveAsync(restarted, manager, inspection, approval)).Receipt);
        Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize((await reports.ReadAsync(group, inspection, 1, CancellationToken.None)).Report));

        await reports.ReopenAsync(manager, inspection, new(Guid.NewGuid(), 1, 1, "Examine repaired bumper again"), CancellationToken.None);
        Assert.Equal(InspectionAssignmentStatus.Conflict,
            (await ApproveAsync(restarted, manager, inspection, renewed with { OperationId = Guid.NewGuid() })).Status);
        Assert.Equal(receipt, (await ApproveAsync(restarted, manager, inspection, approval)).Receipt);
        Assert.Equal(3, (await approvals.ReadAsync(group, inspection, 1, CancellationToken.None)).Receipt!.RepairVersion);
        var database = services.GetRequiredService<DealershipDbContext>();
        Assert.Equal(3, await database.InspectionRepairApprovals.CountAsync(item => item.InspectionId == inspection));
        await database.Users.Where(user => user.Id == manager).ExecuteUpdateAsync(update => update.SetProperty(user => user.IsActive, false));
        Assert.Equal(InspectionAssignmentStatus.Forbidden, (await ApproveAsync(restarted, manager, inspection, approval)).Status);
        await database.Users.Where(user => user.Id == manager).ExecuteUpdateAsync(update => update.SetProperty(user => user.IsActive, true));
        await database.Branches.Where(item => item.Id == data.BranchId).ExecuteUpdateAsync(update => update.SetProperty(item => item.IsActive, false));
        Assert.Equal(InspectionAssignmentStatus.Forbidden, (await ApproveAsync(restarted, manager, inspection, approval)).Status);
    }

    private static async Task VerifyApiAsync(WebApplicationFactory<Program> factory, string email, Guid inspection,
        InspectionRepairApprovalRequest request, InspectionRepairApprovalReceipt receipt)
    {
        using var client = factory.CreateClient();
        var path = $"/api/staff/inspection/drafts/{inspection}/repair-approvals";
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(path, request)).StatusCode);
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(Guid.NewGuid(), email, "Password123!"));
        var session = (await login.Content.ReadFromJsonAsync<LoginSession>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        var replay = await client.PostAsJsonAsync(path, request);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.True(replay.Headers.CacheControl?.NoStore);
        Assert.Equal(receipt, await replay.Content.ReadFromJsonAsync<InspectionRepairApprovalReceipt>());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path, request with { RequestId = Guid.Empty })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(path, request with { RepairVersion = 2 })).StatusCode);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge,
            (await client.PostAsync(path, new StringContent(new string('a', 512 * 1024 + 1), Encoding.UTF8, "application/json"))).StatusCode);
        var read = await client.GetAsync($"/api/staff/inspection/reports/{inspection}/revisions/1/repair-approvals");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.True(read.Headers.CacheControl?.NoStore);
        Assert.Equal(receipt, await read.Content.ReadFromJsonAsync<InspectionRepairApprovalReceipt>());
    }

    private static async Task<(Guid Id, string Email)> ProvisionAsync(IServiceProvider services, Guid? branch, string role)
    {
        var id = Guid.NewGuid();
        var email = $"hafiz.rahman.{id:N}@example.invalid";
        await services.GetRequiredService<StaffProvisioningService>().ProvisionStaffAsync(new(id, email, "Password123!", role, branch), CancellationToken.None);
        return (id, email);
    }

    private static async Task<InspectionRepairApprovalResult> ApproveAsync(WebApplicationFactory<Program> factory, Guid actor,
        Guid inspection, InspectionRepairApprovalRequest request)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IInspectionRepairApprovalStore>()
            .ApproveAsync(actor, inspection, request, CancellationToken.None);
    }
}

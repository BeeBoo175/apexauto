using System.Data;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
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

public sealed class InspectionRepairCompletionTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task CompletionRetainsAssignedReinspectionAndCoveringApprovalAcrossRenewalAndRestart()
    {
        using var data = await CheckInTestData.CreateAsync(fixture);
        var appointment = await data.BookAsync();
        using var scope = data.Factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var checkedIn = await services.GetRequiredService<AppointmentCheckInService>()
            .CheckInAsync(data.StaffId, appointment.Id, CheckInTestData.Evidence, CancellationToken.None);
        var (technician, email) = await ProvisionAsync(services, data.BranchId, AccountRoles.WorkshopTechnician);
        var (colleague, _) = await ProvisionAsync(services, data.BranchId, AccountRoles.WorkshopTechnician);
        var (manager, _) = await ProvisionAsync(services, data.BranchId, AccountRoles.BranchManager);
        var (group, _) = await ProvisionAsync(services, null, AccountRoles.GeneralManager);
        var (admin, _) = await ProvisionAsync(services, null, AccountRoles.Administrator);
        var branch = Guid.NewGuid();
        await services.GetRequiredService<BranchProvisioningService>().ProvisionBranchAsync(
            DevelopmentFixtureManifest.Branches[1] with { Id = branch }, CancellationToken.None);
        var (outside, _) = await ProvisionAsync(services, branch, AccountRoles.WorkshopTechnician);
        var assigned = await services.GetRequiredService<InspectionAssignmentService>().AssignAsync(technician,
            checkedIn.Receipt!.VehicleId, new(Guid.NewGuid(), technician, 0, null), CancellationToken.None);
        var inspection = assigned.Receipt!.InspectionId;
        var save = new InspectionDraftSaveRequest(Guid.NewGuid(), 1, 0,
            [new("EP-004", InspectionEntryKind.Finding, InspectionSeverity.MinorDefect, 250m, "Scratched front bumper")])
        {
            ExaminedPointCodes = [.. InspectionCatalog.Points.Select(point => point.Code)]
        };
        await services.GetRequiredService<InspectionDraftSaveService>().SaveAsync(technician, inspection, save, CancellationToken.None);
        var reports = services.GetRequiredService<IInspectionReportStore>();
        var reopenings = services.GetRequiredService<InspectionReopeningService>();
        var signoffs = services.GetRequiredService<InspectionSignoffService>();
        var original = (await signoffs.SignAsync(technician, inspection, new(Guid.NewGuid(), 1, 1, 1), CancellationToken.None)).Report!;
        var repairs = services.GetRequiredService<IInspectionRepairRequestStore>();
        var request = new InspectionRepairRequest(Guid.NewGuid(), 1, 1, 0, [new("EP-004", "Refinish bumper")], 250m);
        var repair = (await repairs.RequestAsync(technician, inspection, request, CancellationToken.None)).Receipt!;
        var completion = new InspectionRepairCompletionRequest(Guid.NewGuid(), repair.OperationId, 1, 1, 1,
            " Bumper refinished and reinspected; fit and finish satisfactory. ");
        Assert.Equal(InspectionAssignmentStatus.Conflict, (await CompleteAsync(data.Factory, technician, inspection, completion)).Status);
        foreach (var actor in new[] { manager, group, admin, data.StaffId, data.OwnerId })
        {
            Assert.Equal(InspectionAssignmentStatus.Forbidden, (await CompleteAsync(data.Factory, actor, inspection, completion)).Status);
        }
        foreach (var actor in new[] { colleague, outside })
        {
            Assert.Equal(InspectionAssignmentStatus.NotFound, (await CompleteAsync(data.Factory, actor, inspection, completion)).Status);
        }
        foreach (var notes in new string?[] { null, " ", "line\nbreak", new('a', 1001) })
        {
            Assert.Equal(InspectionAssignmentStatus.Invalid,
                (await CompleteAsync(data.Factory, technician, inspection, completion with { Notes = notes })).Status);
        }

        var approval = (await services.GetRequiredService<IInspectionRepairApprovalStore>().ApproveAsync(manager, inspection,
            new(Guid.NewGuid(), repair.OperationId, 1, 1, 1), CancellationToken.None)).Receipt!;
        request = request with { OperationId = Guid.NewGuid(), ExpectedVersion = 1, Budget = 200m };
        repair = (await repairs.RequestAsync(technician, inspection, request, CancellationToken.None)).Receipt!;
        completion = completion with { RequestId = repair.OperationId, RepairVersion = 2 };
        var candidate = new InspectionRepairCompletionReceipt(completion.OperationId, inspection, data.BranchId, technician,
            repair.OperationId, approval.OperationId, 1, 1, 2, completion.Notes!.Trim(), DateTimeOffset.UtcNow);
        await InspectionRepairCompletionSqlAssertions.VerifyBeforeAsync(fixture.Database, candidate, manager, colleague, repair.RequestedAt);
        var race = await Task.WhenAll(CompleteAsync(data.Factory, technician, inspection, completion),
            CompleteAsync(data.Factory, technician, inspection, completion with { OperationId = Guid.NewGuid() }));
        var receipt = Assert.Single(race, result => result.Status == InspectionAssignmentStatus.Succeeded).Receipt!;
        Assert.Single(race, result => result.Status == InspectionAssignmentStatus.Conflict);
        Assert.Equal(approval.OperationId, receipt.ApprovalId);
        completion = completion with { OperationId = receipt.OperationId };
        await InspectionRepairCompletionSqlAssertions.VerifyAfterAsync(fixture.Database, receipt);
        using var restarted = fixture.CreateFactory();
        Assert.Equal(receipt, (await CompleteAsync(restarted, technician, inspection, completion)).Receipt);
        Assert.Equal(InspectionAssignmentStatus.Conflict,
            (await CompleteAsync(restarted, technician, inspection, completion with { Notes = "Changed evidence" })).Status);
        await VerifyApiAsync(restarted, email, inspection, completion, receipt);
        var history = await services.GetRequiredService<IInspectionHistoryStore>()
            .RepairsAsync(group, inspection, 1, new(), CancellationToken.None);
        Assert.Equal(receipt, history.History!.Items[0].Completion);
        Assert.Null(history.History.Items[0].Approval);
        var queues = services.GetRequiredService<IInspectionQueueStore>();
        var completed = Assert.Single((await queues.RepairsAsync(manager, new(), InspectionRepairQueueStatus.Completed, CancellationToken.None)).Queue!.Items);
        Assert.Equal(receipt.OperationId, completed.CompletionId);
        Assert.Equal(approval.OperationId, completed.CoveringApprovalId);

        await using var stale = new NpgsqlConnection(fixture.Database.RuntimeConnectionString);
        await stale.OpenAsync();
        await using var snapshot = await stale.BeginTransactionAsync(IsolationLevel.RepeatableRead);
        await using (var read = new NpgsqlCommand("SELECT \"RepairVersion\" FROM \"InspectionDrafts\" WHERE \"Id\" = @id", stale, snapshot))
        {
            read.Parameters.AddWithValue("id", inspection);
            Assert.Equal(2L, await read.ExecuteScalarAsync());
        }
        request = request with { OperationId = Guid.NewGuid(), ExpectedVersion = 2, Budget = 300m };
        repair = (await repairs.RequestAsync(technician, inspection, request, CancellationToken.None)).Receipt!;
        await InspectionRepairCompletionSqlAssertions.VerifyStaleAsync(stale, snapshot, receipt with { OperationId = Guid.NewGuid() });
        await snapshot.RollbackAsync();
        var fresh = completion with { OperationId = Guid.NewGuid(), RequestId = repair.OperationId, RepairVersion = 3 };
        Assert.Equal(InspectionAssignmentStatus.Conflict, (await CompleteAsync(restarted, technician, inspection, fresh)).Status);
        await InspectionRepairCompletionSqlAssertions.ExpectAsync(fixture.Database, receipt with
        {
            OperationId = fresh.OperationId,
            RequestId = fresh.RequestId,
            RepairVersion = 3,
            CompletedAt = DateTimeOffset.UtcNow
        }, "CK_InspectionRepairCompletions_Approval");
        request = request with { OperationId = Guid.NewGuid(), ExpectedVersion = 3, Budget = 100m, Items = [new("EP-004", "Replace bumper")] };
        repair = (await repairs.RequestAsync(technician, inspection, request, CancellationToken.None)).Receipt!;
        fresh = fresh with { OperationId = Guid.NewGuid(), RequestId = repair.OperationId, RepairVersion = 4 };
        Assert.Equal(InspectionAssignmentStatus.Conflict, (await CompleteAsync(restarted, technician, inspection, fresh)).Status);
        await InspectionRepairCompletionSqlAssertions.ExpectAsync(fixture.Database, receipt with
        {
            OperationId = fresh.OperationId,
            RequestId = fresh.RequestId,
            RepairVersion = 4,
            CompletedAt = DateTimeOffset.UtcNow
        }, "CK_InspectionRepairCompletions_Approval");

        request = request with { OperationId = Guid.NewGuid(), ExpectedVersion = 4, Budget = 0m };
        repair = (await repairs.RequestAsync(technician, inspection, request, CancellationToken.None)).Receipt!;
        fresh = fresh with { OperationId = Guid.NewGuid(), RequestId = repair.OperationId, RepairVersion = 5 };
        var free = (await CompleteAsync(restarted, technician, inspection, fresh)).Receipt!;
        Assert.Null(free.ApprovalId);
        Assert.Equal(receipt, (await CompleteAsync(restarted, technician, inspection, completion)).Receipt);
        Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize((await reports.ReadAsync(group, inspection, 1, CancellationToken.None)).Report));
        await reopenings.ReopenAsync(manager, inspection, new(Guid.NewGuid(), 1, 1, "Examine the retained repair evidence again"), CancellationToken.None);
        Assert.Equal(InspectionAssignmentStatus.Conflict,
            (await CompleteAsync(restarted, technician, inspection, fresh with { OperationId = Guid.NewGuid() })).Status);
        Assert.Equal(free, (await CompleteAsync(restarted, technician, inspection, fresh)).Receipt);
        Assert.Empty((await queues.RepairsAsync(manager, new(), InspectionRepairQueueStatus.Completed, CancellationToken.None)).Queue!.Items);
        var database = services.GetRequiredService<DealershipDbContext>();
        await database.Users.Where(item => item.Id == technician).ExecuteUpdateAsync(update => update.SetProperty(item => item.IsActive, false));
        Assert.Equal(InspectionAssignmentStatus.Forbidden, (await CompleteAsync(restarted, technician, inspection, completion)).Status);
        await database.Users.Where(item => item.Id == technician).ExecuteUpdateAsync(update => update.SetProperty(item => item.IsActive, true));
        await database.Branches.Where(item => item.Id == data.BranchId).ExecuteUpdateAsync(update => update.SetProperty(item => item.IsActive, false));
        Assert.Equal(InspectionAssignmentStatus.Forbidden, (await CompleteAsync(restarted, technician, inspection, completion)).Status);
    }

    private static async Task VerifyApiAsync(WebApplicationFactory<Program> factory, string email, Guid inspection,
        InspectionRepairCompletionRequest request, InspectionRepairCompletionReceipt receipt)
    {
        using var client = factory.CreateClient();
        var path = $"/api/staff/inspection/drafts/{inspection}/repair-completions";
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(path, request)).StatusCode);
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(Guid.NewGuid(), email, "Password123!"));
        var session = (await login.Content.ReadFromJsonAsync<LoginSession>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        var replay = await client.PostAsJsonAsync(path, request);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.True(replay.Headers.CacheControl?.NoStore);
        Assert.Equal(receipt, await replay.Content.ReadFromJsonAsync<InspectionRepairCompletionReceipt>());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path, request with { Notes = " " })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(path, request with { RepairVersion = 1 })).StatusCode);
    }

    private static async Task<(Guid Id, string Email)> ProvisionAsync(IServiceProvider services, Guid? branch, string role)
    {
        var id = Guid.NewGuid();
        var email = $"hafiz.rahman.{id:N}@example.invalid";
        await services.GetRequiredService<StaffProvisioningService>().ProvisionStaffAsync(new(id, email, "Password123!", role, branch), CancellationToken.None);
        return (id, email);
    }

    private static async Task<InspectionRepairCompletionResult> CompleteAsync(WebApplicationFactory<Program> factory,
        Guid actor, Guid inspection, InspectionRepairCompletionRequest request)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IInspectionRepairCompletionStore>()
            .CompleteAsync(actor, inspection, request, CancellationToken.None);
    }
}

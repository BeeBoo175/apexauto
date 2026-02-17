using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Dealership.Application.Accounts;
using Dealership.Application.Inspections;
using Dealership.Application.Intake;
using Dealership.Domain.Inspections;
using Dealership.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dealership.IntegrationTests;

public sealed class InspectionDraftSaveTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task SavesRetainExactReceiptsAcrossRacesRestartEditsAndAssignmentChanges()
    {
        using var data = await CheckInTestData.CreateAsync(fixture);
        var appointment = await data.BookAsync();
        using var scope = data.Factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var checkIn = await services.GetRequiredService<IAppointmentCheckInStore>()
            .CheckInAsync(data.StaffId, appointment.Id, CheckInTestData.Evidence, CancellationToken.None);
        Assert.Equal(CheckInStatus.Succeeded, checkIn.Status);
        var vehicleId = checkIn.Receipt!.VehicleId;
        var technician = await ProvisionAsync(services, data.BranchId, AccountRoles.WorkshopTechnician);
        var replacement = await ProvisionAsync(services, data.BranchId, AccountRoles.WorkshopTechnician);
        var manager = await ProvisionAsync(services, data.BranchId, AccountRoles.BranchManager);
        var group = await ProvisionAsync(services, null, AccountRoles.GeneralManager);
        var assignment = await services.GetRequiredService<IInspectionAssignmentStore>().AssignAsync(technician.Id, vehicleId,
            new(Guid.NewGuid(), technician.Id, 0, null), CancellationToken.None);
        var inspectionId = assignment.Receipt!.InspectionId;
        var request = new InspectionDraftSaveRequest(Guid.NewGuid(), 1, 0,
        [
            new("IE-040", InspectionEntryKind.NotFitted, null, 0, " No powered roof is fitted "),
            new("EP-004", InspectionEntryKind.Finding, InspectionSeverity.MinorDefect, 500m, "Bumper scratch")
        ]);
        var race = await Task.WhenAll(SaveAsync(data.Factory, technician.Id, inspectionId, request),
            SaveAsync(data.Factory, technician.Id, inspectionId, request with { OperationId = Guid.NewGuid() }));
        var receipt = Assert.Single(race, result => result.Status == InspectionAssignmentStatus.Succeeded).Receipt!;
        Assert.Single(race, result => result.Status == InspectionAssignmentStatus.Conflict);
        request = request with { OperationId = receipt.OperationId };
        Assert.Equal(InspectionGrade.B, receipt.Assessment.Grade);
        Assert.Equal(500m, receipt.Assessment.EstimatedRepairCost);
        Assert.Equal("No powered roof is fitted", receipt.Entries[1].Notes);
        using var restarted = fixture.CreateFactory();
        var replay = await Task.WhenAll(SaveAsync(restarted, technician.Id, inspectionId, request),
            SaveAsync(restarted, technician.Id, inspectionId, request with { Entries = request.Entries!.Reverse().ToArray() }));
        Assert.All(replay, result => EqualReceipt(receipt, result.Receipt));
        Assert.Equal(InspectionAssignmentStatus.Conflict, (await SaveAsync(restarted, technician.Id, inspectionId,
            request with { Entries = [] })).Status);
        Assert.Equal(InspectionAssignmentStatus.Conflict, (await SaveAsync(restarted, technician.Id, inspectionId,
            request with { OperationId = Guid.NewGuid(), AssignmentVersion = 2 })).Status);

        var cleared = await SaveAsync(restarted, technician.Id, inspectionId, new(Guid.NewGuid(), 1, 1, []));
        Assert.Equal(InspectionAssignmentStatus.Succeeded, cleared.Status);
        Assert.Equal(2, cleared.Receipt!.Version);
        Assert.Empty(cleared.Receipt.Entries);
        EqualReceipt(receipt, (await SaveAsync(restarted, technician.Id, inspectionId, request)).Receipt);
        foreach (var reader in new[] { technician.Id, manager.Id, group.Id })
        {
            using var readScope = restarted.Services.CreateScope();
            var read = await readScope.ServiceProvider.GetRequiredService<IInspectionAssignmentStore>()
                .ReadAsync(reader, inspectionId, CancellationToken.None);
            EqualReceipt(cleared.Receipt, read.Draft!.LatestSave);
        }
        foreach (var actor in new[] { manager.Id, group.Id, data.StaffId, data.OwnerId })
        {
            Assert.Equal(InspectionAssignmentStatus.Forbidden, (await SaveAsync(restarted, actor, inspectionId, request)).Status);
        }
        Assert.Equal(InspectionAssignmentStatus.NotFound, (await SaveAsync(restarted, replacement.Id, inspectionId, request)).Status);
        await AssertInvalidAsync(restarted, technician.Id, inspectionId, request);
        await InspectionDraftSaveSqlAssertions.VerifyAsync(fixture.Database, cleared.Receipt, data.BranchId, replacement.Id, manager.Id);
        await AssertApiAsync(restarted, technician.Email, inspectionId, request, receipt);

        using var changeScope = restarted.Services.CreateScope();
        var changeServices = changeScope.ServiceProvider;
        var reassigned = await changeServices.GetRequiredService<IInspectionAssignmentStore>().AssignAsync(manager.Id,
            vehicleId, new(Guid.NewGuid(), replacement.Id, 1, "Workshop handover"), CancellationToken.None);
        Assert.Equal(InspectionAssignmentStatus.Succeeded, reassigned.Status);
        Assert.Equal(InspectionAssignmentStatus.NotFound, (await SaveAsync(restarted, technician.Id, inspectionId, request)).Status);
        Assert.Equal(InspectionAssignmentStatus.Conflict,
            (await SaveAsync(restarted, replacement.Id, inspectionId, new(Guid.NewGuid(), 1, 3, []))).Status);
        var newOwner = await SaveAsync(restarted, replacement.Id, inspectionId, new(Guid.NewGuid(), 2, 3, []));
        Assert.Equal(InspectionAssignmentStatus.Succeeded, newOwner.Status);

        var database = changeServices.GetRequiredService<DealershipDbContext>();
        await database.Users.Where(user => user.Id == replacement.Id).ExecuteUpdateAsync(update => update.SetProperty(user => user.IsActive, false));
        Assert.Equal(InspectionAssignmentStatus.Forbidden,
            (await SaveAsync(restarted, replacement.Id, inspectionId, new(Guid.NewGuid(), 2, 4, []))).Status);
        await database.Users.Where(user => user.Id == replacement.Id).ExecuteUpdateAsync(update => update
            .SetProperty(user => user.IsActive, true).SetProperty(user => user.BranchId, data.BranchId));
        await database.Branches.Where(branch => branch.Id == data.BranchId).ExecuteUpdateAsync(update => update.SetProperty(branch => branch.IsActive, false));
        Assert.Equal(InspectionAssignmentStatus.Forbidden,
            (await SaveAsync(restarted, replacement.Id, inspectionId, new(Guid.NewGuid(), 2, 4, []))).Status);
        Assert.Equal(4, await database.InspectionDraftSaves.CountAsync());
    }

    private static async Task AssertInvalidAsync(WebApplicationFactory<Program> factory, Guid actor, Guid inspection,
        InspectionDraftSaveRequest request)
    {
        foreach (var invalid in new[]
        {
            request with { OperationId = Guid.Empty }, request with { AssignmentVersion = 0 },
            request with { ExpectedVersion = long.MaxValue }, request with { ExpectedVersion = -1 },
            request with { Entries = null }, request with { Entries = [null!] },
            request with { Entries = [request.Entries![0], request.Entries[0]] },
            request with { Entries = [new("FC-001", InspectionEntryKind.Finding, InspectionSeverity.Advisory, 0.001m, "Observed")] },
            request with { Entries = [new("FC-001", InspectionEntryKind.NotFitted, null, 1m, "Absent")] },
            request with { Entries = [new("UNKNOWN", InspectionEntryKind.NotFitted, null, 0, "Absent")] }
        })
        {
            Assert.Equal(InspectionAssignmentStatus.Invalid, (await SaveAsync(factory, actor, inspection, invalid)).Status);
        }
    }

    private static async Task AssertApiAsync(WebApplicationFactory<Program> factory, string email, Guid inspection,
        InspectionDraftSaveRequest original, InspectionDraftSaveReceipt receipt)
    {
        using var client = factory.CreateClient();
        var path = $"/api/staff/inspection/drafts/{inspection}/saves";
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(path, original)).StatusCode);
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(Guid.NewGuid(), email, "Password123!"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var session = (await login.Content.ReadFromJsonAsync<LoginSession>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        var replay = await client.PostAsJsonAsync(path, original);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.True(replay.Headers.CacheControl?.NoStore);
        EqualReceipt(receipt, await replay.Content.ReadFromJsonAsync<InspectionDraftSaveReceipt>());
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync(path, original with { Entries = [null!] })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await client.PostAsJsonAsync(path, original with { Entries = [] })).StatusCode);
        foreach (var estimate in new[] { "0.010000000000000000000000000001", "1e-100", "7922816251426433759354395033.51" })
        {
            using var raw = new StringContent($$"""
                {"operationId":"{{Guid.NewGuid()}}","assignmentVersion":1,"expectedVersion":2,
                 "entries":[{"pointCode":"EP-004","kind":0,"severity":0,"estimatedRepairCost":{{estimate}},"notes":"Observed"}]}
                """, Encoding.UTF8, "application/json");
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync(path, raw)).StatusCode);
        }
        using var oversized = new StringContent(new string('x', 512 * 1024 + 1), Encoding.UTF8, "application/json");
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (await client.PostAsync(path, oversized)).StatusCode);

        var populated = new InspectionDraftSaveRequest(Guid.NewGuid(), 1, 2, InspectionCatalog.Points.Select(point =>
            new InspectionDraftEntryInput(point.Code, InspectionEntryKind.Finding, InspectionSeverity.Advisory, 0, new string('x', 1000))).ToArray());
        Assert.True(JsonSerializer.Serialize(populated).Length > 16 * 1024);
        var saved = await client.PostAsJsonAsync(path, populated);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var latest = (await saved.Content.ReadFromJsonAsync<InspectionDraftSaveReceipt>())!;
        Assert.Equal(210, latest.Entries.Count);
        Assert.Equal(3, latest.Version);
        var read = await client.GetAsync($"/api/staff/inspection/drafts/{inspection}");
        Assert.True(read.Headers.CacheControl?.NoStore);
        EqualReceipt(latest, (await read.Content.ReadFromJsonAsync<InspectionDraftSnapshot>())!.LatestSave);
    }

    private static void EqualReceipt(InspectionDraftSaveReceipt expected, InspectionDraftSaveReceipt? actual)
    {
        Assert.NotNull(actual);
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
    }

    private static async Task<(Guid Id, string Email)> ProvisionAsync(IServiceProvider services, Guid? branch, string role)
    {
        var id = Guid.NewGuid();
        var email = $"arif.hakimi.{id:N}@example.invalid";
        await services.GetRequiredService<StaffProvisioningService>().ProvisionStaffAsync(
            new StaffProvisioningRequest(id, email, "Password123!", role, branch), CancellationToken.None);
        return (id, email);
    }

    private static async Task<InspectionDraftSaveResult> SaveAsync(WebApplicationFactory<Program> factory,
        Guid actor, Guid inspection, InspectionDraftSaveRequest request)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IInspectionDraftSaveStore>()
            .SaveAsync(actor, inspection, request, CancellationToken.None);
    }
}

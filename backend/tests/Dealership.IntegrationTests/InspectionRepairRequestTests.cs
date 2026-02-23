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

public sealed class InspectionRepairRequestTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task RepairRequestsRetainExactScopeAndBudgetAcrossRacesReopeningAndReconstruction()
    {
        using var data = await CheckInTestData.CreateAsync(fixture);
        var appointment = await data.BookAsync();
        using var scope = data.Factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var checkIn = await services.GetRequiredService<AppointmentCheckInService>()
            .CheckInAsync(data.StaffId, appointment.Id, CheckInTestData.Evidence, CancellationToken.None);
        var (technician, email) = await ProvisionAsync(services, data.BranchId, AccountRoles.WorkshopTechnician);
        var (manager, _) = await ProvisionAsync(services, data.BranchId, AccountRoles.BranchManager);
        var (group, _) = await ProvisionAsync(services, null, AccountRoles.GeneralManager);
        var (admin, _) = await ProvisionAsync(services, null, AccountRoles.Administrator);
        var (colleague, _) = await ProvisionAsync(services, data.BranchId, AccountRoles.WorkshopTechnician);
        var branch = Guid.NewGuid();
        await services.GetRequiredService<BranchProvisioningService>().ProvisionBranchAsync(
            DevelopmentFixtureManifest.Branches[1] with { Id = branch }, CancellationToken.None);
        var (outside, _) = await ProvisionAsync(services, branch, AccountRoles.WorkshopTechnician);
        var claim = await services.GetRequiredService<IInspectionAssignmentStore>().AssignAsync(technician,
            checkIn.Receipt!.VehicleId, new(Guid.NewGuid(), technician, 0, null), CancellationToken.None);
        var inspection = claim.Receipt!.InspectionId;
        var request = new InspectionRepairRequest(Guid.NewGuid(), 1, 1, 0, [new("EP-004", "Refinish front bumper")], 250m);
        Assert.Equal(InspectionAssignmentStatus.Conflict, (await RequestAsync(data.Factory, technician, inspection, request)).Status);
        var save = new InspectionDraftSaveRequest(Guid.NewGuid(), 1, 0,
            [new("EP-004", InspectionEntryKind.Finding, InspectionSeverity.MinorDefect, 250m, "Scratched bumper surface")])
        {
            ExaminedPointCodes = [.. InspectionCatalog.Points.Select(point => point.Code)]
        };
        var saves = services.GetRequiredService<IInspectionDraftSaveStore>();
        Assert.Equal(InspectionAssignmentStatus.Succeeded, (await saves.SaveAsync(technician, inspection, save, CancellationToken.None)).Status);
        var reports = services.GetRequiredService<IInspectionReportStore>();
        var original = (await reports.SignAsync(technician, inspection, new(Guid.NewGuid(), 1, 1, 1), CancellationToken.None)).Report!;
        var repairs = services.GetRequiredService<IInspectionRepairRequestStore>();
        foreach (var actor in new[] { manager, group, admin, data.StaffId, data.OwnerId })
        {
            Assert.Equal(InspectionAssignmentStatus.Forbidden, (await RequestAsync(data.Factory, actor, inspection, request)).Status);
        }
        foreach (var actor in new[] { colleague, outside })
        {
            Assert.Equal(InspectionAssignmentStatus.NotFound, (await RequestAsync(data.Factory, actor, inspection, request)).Status);
        }
        Assert.Equal(InspectionAssignmentStatus.Invalid,
            (await RequestAsync(data.Factory, technician, inspection, request with { Items = [new("IE-040", "Replace absent roof")] })).Status);
        Assert.Equal(InspectionAssignmentStatus.Conflict,
            (await RequestAsync(data.Factory, technician, inspection, request with { AssignmentVersion = 2 })).Status);
        await InspectionRepairRequestSqlAssertions.VerifyBeforeAsync(fixture.Database, original, manager, colleague, outside);

        await using var stale = new NpgsqlConnection(fixture.Database.RuntimeConnectionString);
        await stale.OpenAsync();
        await using var oldSnapshot = await stale.BeginTransactionAsync(IsolationLevel.RepeatableRead);
        await using (var read = new NpgsqlCommand("SELECT \"RepairVersion\" FROM \"InspectionDrafts\" WHERE \"Id\" = @inspection", stale, oldSnapshot))
        {
            read.Parameters.AddWithValue("inspection", inspection);
            Assert.Equal(0L, await read.ExecuteScalarAsync());
        }
        var race = await Task.WhenAll(RequestAsync(data.Factory, technician, inspection, request),
            RequestAsync(data.Factory, technician, inspection, request with { OperationId = Guid.NewGuid() }));
        var receipt = Assert.Single(race, result => result.Status == InspectionAssignmentStatus.Succeeded).Receipt!;
        Assert.Single(race, result => result.Status == InspectionAssignmentStatus.Conflict);
        request = request with { OperationId = receipt.OperationId };
        await InspectionReopeningSqlAssertions.VerifyOldSnapshotAsync(stale, oldSnapshot, inspection);
        await oldSnapshot.RollbackAsync();
        await InspectionRepairRequestSqlAssertions.VerifyAfterAsync(fixture.Database, receipt);

        using var restarted = fixture.CreateFactory();
        var replay = await Task.WhenAll(RequestAsync(restarted, technician, inspection, request),
            RequestAsync(restarted, technician, inspection, request with { Items = [new("EP-004", "  Refinish front bumper  ")] }));
        Assert.All(replay, result => Assert.Equal(JsonSerializer.Serialize(receipt), JsonSerializer.Serialize(result.Receipt)));
        Assert.Equal(InspectionAssignmentStatus.Conflict,
            (await RequestAsync(restarted, technician, inspection, request with { Budget = 251m })).Status);
        var changed = request with { OperationId = Guid.NewGuid(), ExpectedVersion = 1, Budget = 400m, Items = [new("EP-004", "Replace bumper cover")] };
        Assert.Equal(2, (await RequestAsync(restarted, technician, inspection, changed)).Receipt!.Version);
        foreach (var actor in new[] { technician, manager, group })
        {
            Assert.Equal(2, (await repairs.ReadAsync(actor, inspection, 1, CancellationToken.None)).Receipt!.Version);
        }
        Assert.Equal(InspectionAssignmentStatus.NotFound, (await repairs.ReadAsync(outside, inspection, 1, CancellationToken.None)).Status);
        Assert.Equal(InspectionAssignmentStatus.NotFound, (await repairs.ReadAsync(colleague, inspection, 1, CancellationToken.None)).Status);
        Assert.Equal(InspectionAssignmentStatus.Forbidden, (await repairs.ReadAsync(admin, inspection, 1, CancellationToken.None)).Status);
        Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize((await reports.ReadAsync(group, inspection, 1, CancellationToken.None)).Report));
        await VerifyApiAsync(restarted, email, inspection, request, receipt);

        Assert.Equal(InspectionAssignmentStatus.Succeeded, (await reports.ReopenAsync(manager, inspection,
            new(Guid.NewGuid(), 1, 1, "Verify revised condition before further repairs"), CancellationToken.None)).Status);
        Assert.Equal(InspectionAssignmentStatus.Conflict,
            (await RequestAsync(restarted, technician, inspection, changed with { OperationId = Guid.NewGuid(), ExpectedVersion = 2 })).Status);
        Assert.Equal(JsonSerializer.Serialize(receipt), JsonSerializer.Serialize((await RequestAsync(restarted, technician, inspection, request)).Receipt));
        save = save with
        {
            OperationId = Guid.NewGuid(),
            ExpectedVersion = 1,
            Entries = [new("FC-042", InspectionEntryKind.Finding, InspectionSeverity.CriticalDisqualifier, 0, "Flood evidence confirmed")]
        };
        Assert.Equal(InspectionAssignmentStatus.Succeeded, (await saves.SaveAsync(technician, inspection, save, CancellationToken.None)).Status);
        var rejected = (await reports.SignAsync(technician, inspection, new(Guid.NewGuid(), 1, 2, 2), CancellationToken.None)).Report!;
        Assert.True(rejected.Assessment.IsRejected);
        var rejectedRequest = new InspectionRepairRequest(Guid.NewGuid(), 1, 2, 2, [new("FC-042", "Dry cabin")], 100m);
        Assert.Equal(InspectionAssignmentStatus.Conflict, (await RequestAsync(restarted, technician, inspection, rejectedRequest)).Status);
        await InspectionRepairRequestSqlAssertions.VerifyRejectedRevisionAsync(fixture.Database, rejected, 2);
        var database = services.GetRequiredService<DealershipDbContext>();
        await database.Users.Where(user => user.Id == technician).ExecuteUpdateAsync(update => update.SetProperty(user => user.IsActive, false));
        Assert.Equal(InspectionAssignmentStatus.Forbidden, (await RequestAsync(restarted, technician, inspection, request)).Status);
        Assert.Equal(InspectionAssignmentStatus.Forbidden, (await repairs.ReadAsync(technician, inspection, 1, CancellationToken.None)).Status);
        await database.Users.Where(user => user.Id == technician).ExecuteUpdateAsync(update => update.SetProperty(user => user.IsActive, true));
        await database.Branches.Where(item => item.Id == data.BranchId).ExecuteUpdateAsync(update => update.SetProperty(item => item.IsActive, false));
        Assert.Equal(InspectionAssignmentStatus.Forbidden, (await RequestAsync(restarted, technician, inspection, request)).Status);
    }

    private static async Task VerifyApiAsync(WebApplicationFactory<Program> factory, string email, Guid inspection,
        InspectionRepairRequest request, InspectionRepairReceipt receipt)
    {
        using var client = factory.CreateClient();
        var path = $"/api/staff/inspection/drafts/{inspection}/repair-requests";
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(path, request)).StatusCode);
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(Guid.NewGuid(), email, "Password123!"));
        var session = (await login.Content.ReadFromJsonAsync<LoginSession>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        var replay = await client.PostAsJsonAsync(path, request);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.True(replay.Headers.CacheControl?.NoStore);
        Assert.Equal(JsonSerializer.Serialize(receipt), JsonSerializer.Serialize(await replay.Content.ReadFromJsonAsync<InspectionRepairReceipt>()));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path, request with { Items = null! })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(path, request with { Budget = 251m })).StatusCode);
        var raw = JsonSerializer.Serialize(request, JsonSerializerOptions.Web).Replace("\"budget\":250", "\"budget\":0.000000000000000000000000000001", StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync(path, new StringContent(raw, Encoding.UTF8, "application/json"))).StatusCode);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge,
            (await client.PostAsync(path, new StringContent(new string('a', 512 * 1024 + 1), Encoding.UTF8, "application/json"))).StatusCode);
        var read = await client.GetAsync($"/api/staff/inspection/reports/{inspection}/revisions/1/repairs");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.True(read.Headers.CacheControl?.NoStore);
        Assert.Equal(2, (await read.Content.ReadFromJsonAsync<InspectionRepairReceipt>())!.Version);
    }

    private static async Task<(Guid Id, string Email)> ProvisionAsync(IServiceProvider services, Guid? branch, string role)
    {
        var id = Guid.NewGuid();
        var email = $"azlan.hakimi.{id:N}@example.invalid";
        await services.GetRequiredService<StaffProvisioningService>().ProvisionStaffAsync(new(id, email, "Password123!", role, branch), CancellationToken.None);
        return (id, email);
    }

    private static async Task<InspectionRepairResult> RequestAsync(WebApplicationFactory<Program> factory, Guid actor,
        Guid inspection, InspectionRepairRequest request)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IInspectionRepairRequestStore>()
            .RequestAsync(actor, inspection, request, CancellationToken.None);
    }
}

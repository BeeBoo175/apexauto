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

namespace Dealership.IntegrationTests;

public sealed class InspectionAssignmentTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task ClaimsRaceReconcileAcrossRestartAndRequireCurrentScopedManagerForReassignment()
    {
        using var data = await CheckInTestData.CreateAsync(fixture);
        var appointment = await data.BookAsync();
        using var scope = data.Factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var checkIn = await services.GetRequiredService<AppointmentCheckInService>()
            .CheckInAsync(data.StaffId, appointment.Id, CheckInTestData.Evidence, CancellationToken.None);
        Assert.Equal(CheckInStatus.Succeeded, checkIn.Status);
        var vehicleId = checkIn.Receipt!.VehicleId;
        var (firstId, _) = await ProvisionAsync(services, data.BranchId, AccountRoles.WorkshopTechnician, "azlan.rahim");
        var (secondId, _) = await ProvisionAsync(services, data.BranchId, AccountRoles.WorkshopTechnician, "arif.hakimi");
        var (managerId, managerEmail) = await ProvisionAsync(services, data.BranchId, AccountRoles.BranchManager, "siti.aminah");
        var (groupId, _) = await ProvisionAsync(services, null, AccountRoles.GeneralManager, "farah.aziz");
        var claims = new[]
        {
            new InspectionAssignmentRequest(Guid.NewGuid(), firstId, 0, null),
            new InspectionAssignmentRequest(Guid.NewGuid(), secondId, 0, null)
        };
        var competing = await Task.WhenAll(claims.Select(request => AssignAsync(data.Factory, request.TechnicianId, vehicleId, request)));
        var won = Array.FindIndex(competing, result => result.Status == InspectionAssignmentStatus.Succeeded);
        Assert.Equal(1, competing.Count(result => result.Status == InspectionAssignmentStatus.Succeeded));
        Assert.Equal(1, competing.Count(result => result.Status == InspectionAssignmentStatus.Conflict));
        var claim = claims[won];
        var receipt = competing[won].Receipt!;
        var replacement = claims[1 - won].TechnicianId;
        Assert.Equal(claim.OperationId, receipt.InspectionId);
        Assert.Equal(1, receipt.Version);
        using var restarted = fixture.CreateFactory();
        var replayed = await Task.WhenAll(AssignAsync(restarted, claim.TechnicianId, vehicleId, claim),
            AssignAsync(restarted, claim.TechnicianId, vehicleId, claim));
        Assert.All(replayed, result => Assert.Equal(receipt, result.Receipt));
        Assert.Equal(InspectionAssignmentStatus.Conflict,
            (await AssignAsync(restarted, replacement, vehicleId, claim with { TechnicianId = replacement })).Status);

        var reassignment = new InspectionAssignmentRequest(Guid.NewGuid(), replacement, 1, "  Cover the workshop handover  ");
        Assert.Equal(InspectionAssignmentStatus.Forbidden,
            (await AssignAsync(restarted, firstId, vehicleId, reassignment)).Status);
        var changes = await Task.WhenAll(AssignAsync(restarted, managerId, vehicleId, reassignment),
            AssignAsync(restarted, managerId, vehicleId, reassignment with { OperationId = Guid.NewGuid() }));
        Assert.Single(changes, result => result.Status == InspectionAssignmentStatus.Succeeded);
        Assert.Single(changes, result => result.Status == InspectionAssignmentStatus.Conflict);
        var changed = changes.Single(result => result.Status == InspectionAssignmentStatus.Succeeded).Receipt!;
        Assert.Equal(2, changed.Version);
        Assert.Equal(claim.TechnicianId, changed.PreviousTechnicianId);
        Assert.Equal("Cover the workshop handover", changed.Reason);
        var exact = reassignment with { OperationId = changed.OperationId };
        Assert.Equal(changed, (await AssignAsync(restarted, managerId, vehicleId, exact)).Receipt);
        Assert.Equal(InspectionAssignmentStatus.Conflict,
            (await AssignAsync(restarted, managerId, vehicleId, exact with { Reason = "Different contents" })).Status);
        Assert.Equal(receipt, (await AssignAsync(restarted, claim.TechnicianId, vehicleId, claim)).Receipt);
        Assert.Equal(InspectionAssignmentStatus.NotFound, (await ReadAsync(restarted, claim.TechnicianId, receipt.InspectionId)).Status);
        Assert.Equal(2, (await ReadAsync(restarted, replacement, receipt.InspectionId)).Draft!.AssignmentVersion);
        Assert.Equal(InspectionAssignmentStatus.Succeeded, (await ReadAsync(restarted, managerId, receipt.InspectionId)).Status);
        Assert.Equal(InspectionAssignmentStatus.Succeeded, (await ReadAsync(restarted, groupId, receipt.InspectionId)).Status);
        Assert.Equal(InspectionAssignmentStatus.Forbidden, (await ReadAsync(restarted, data.StaffId, receipt.InspectionId)).Status);
        var draft = (await ReadAsync(restarted, replacement, receipt.InspectionId)).Draft!;
        Assert.Equal(InspectionCatalog.Version, draft.CatalogVersion);
        Assert.Equal(InspectionCatalog.Points, draft.Catalog);
        Assert.Equal(appointment.RegistrationPlate, draft.Vehicle!.RegistrationPlate);
        Assert.Equal(CheckInTestData.Evidence.Vin, draft.Vehicle.Vin);
        Assert.Equal(CheckInTestData.Evidence.MileageKilometres, draft.Vehicle.MileageKilometres);
        Assert.Equal("PendingInspection", draft.Vehicle.Status);

        var database = services.GetRequiredService<DealershipDbContext>();
        await AssertTechniciansAsync(services, database, managerId, firstId, secondId,
            [firstId, secondId, groupId, data.StaffId, data.OwnerId]);
        Assert.Equal(1, await database.InspectionDrafts.CountAsync());
        Assert.Equal(2, await database.InspectionAssignments.CountAsync());
        await InspectionAssignmentSqlAssertions.VerifyAsync(fixture.Database, receipt, changed, firstId, secondId, managerId);
        await AssertApiAsync(restarted, vehicleId, changed, exact, managerEmail);
        await AssertScopeDenialsAsync(data, database, managerId, replacement, receipt.InspectionId, vehicleId, exact);
    }

    private static async Task AssertApiAsync(WebApplicationFactory<Program> factory, Guid vehicleId,
        InspectionAssignmentReceipt receipt, InspectionAssignmentRequest request, string email)
    {
        using var client = factory.CreateClient();
        var path = $"/api/staff/inspection/vehicles/{vehicleId}/assignment";
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(path, request)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/staff/inspection/technicians")).StatusCode);
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(Guid.NewGuid(), email, "Password123!"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var session = (await login.Content.ReadFromJsonAsync<LoginSession>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        var replay = await client.PostAsJsonAsync(path, request);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.True(replay.Headers.CacheControl?.NoStore);
        Assert.Equal(receipt, await replay.Content.ReadFromJsonAsync<InspectionAssignmentReceipt>());
        var read = await client.GetAsync($"/api/staff/inspection/drafts/{receipt.InspectionId}");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.True(read.Headers.CacheControl?.NoStore);
        Assert.Equal(receipt.Version, (await read.Content.ReadFromJsonAsync<InspectionDraftSnapshot>())!.AssignmentVersion);
        using var json = JsonDocument.Parse(await read.Content.ReadAsStringAsync());
        Assert.Equal(210, json.RootElement.GetProperty("catalog").GetArrayLength());
        Assert.Equal(InspectionCatalog.Version, json.RootElement.GetProperty("catalogVersion").GetString());
        var choices = await client.GetAsync("/api/staff/inspection/technicians");
        Assert.True(choices.Headers.CacheControl?.NoStore);
        var page = (await choices.Content.ReadFromJsonAsync<InspectionTechnicianPage>())!;
        Assert.Equal(25, page.PageSize);
        Assert.Equal(2, page.Total);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/staff/inspection/technicians?pageSize=20")).StatusCode);
        foreach (var invalid in new[]
        {
            request with { OperationId = Guid.Empty }, request with { ExpectedVersion = -1 },
            request with { Reason = "" }, request with { Reason = "line\nbreak" },
            request with { Reason = new string('x', 1001) }
        })
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path, invalid)).StatusCode);
        }
        Assert.Equal(HttpStatusCode.Conflict,
            (await client.PostAsJsonAsync(path, request with { Reason = "Changed request" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.PostAsJsonAsync($"/api/staff/inspection/vehicles/{Guid.NewGuid()}/assignment", request)).StatusCode);
    }

    private static async Task AssertTechniciansAsync(IServiceProvider services, DealershipDbContext database,
        Guid manager, Guid first, Guid second, Guid[] denied)
    {
        var store = services.GetRequiredService<IInspectionAssignmentStore>();
        foreach (var pageSize in new[] { 10, 25, 50 })
        {
            var choices = (await store.TechniciansAsync(manager, 1, pageSize, CancellationToken.None)).Technicians!;
            Assert.Equal(pageSize, choices.PageSize);
            Assert.Equal(2, choices.Total);
            Assert.Equal(new[] { first, second }.Order(), choices.Items.Select(item => item.Id).Order());
            Assert.All(choices.Items, item => Assert.Contains("@example.invalid", item.Identity));
        }
        Assert.Empty((await store.TechniciansAsync(manager, int.MaxValue, 50, CancellationToken.None)).Technicians!.Items);
        Assert.Equal(InspectionAssignmentStatus.Invalid, (await store.TechniciansAsync(manager, 0, 25, CancellationToken.None)).Status);
        Assert.Equal(InspectionAssignmentStatus.Invalid, (await store.TechniciansAsync(manager, 1, 20, CancellationToken.None)).Status);
        foreach (var actor in denied)
        {
            Assert.Equal(InspectionAssignmentStatus.Forbidden, (await store.TechniciansAsync(actor, 1, 25, CancellationToken.None)).Status);
        }
        await database.Users.Where(user => user.Id == first).ExecuteUpdateAsync(update => update.SetProperty(user => user.IsActive, false));
        Assert.Equal(second, Assert.Single((await store.TechniciansAsync(manager, 1, 25, CancellationToken.None)).Technicians!.Items).Id);
        await database.Users.Where(user => user.Id == first).ExecuteUpdateAsync(update => update.SetProperty(user => user.IsActive, true));
        await database.Users.Where(user => user.Id == manager).ExecuteUpdateAsync(update => update.SetProperty(user => user.IsActive, false));
        Assert.Equal(InspectionAssignmentStatus.Forbidden, (await store.TechniciansAsync(manager, 1, 25, CancellationToken.None)).Status);
        await database.Users.Where(user => user.Id == manager).ExecuteUpdateAsync(update => update.SetProperty(user => user.IsActive, true));
    }

    private static async Task AssertScopeDenialsAsync(CheckInTestData data, DealershipDbContext database, Guid managerId,
        Guid technicianId, Guid inspectionId, Guid vehicleId, InspectionAssignmentRequest request)
    {
        var branch = Guid.NewGuid();
        using var scope = data.Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<BranchProvisioningService>().ProvisionBranchAsync(
            DevelopmentFixtureManifest.Branches[0] with { Id = branch, Name = "ApexAuto Petaling Jaya" }, CancellationToken.None);
        await database.Users.Where(user => user.Id == managerId).ExecuteUpdateAsync(update => update.SetProperty(user => user.BranchId, branch));
        var assignments = scope.ServiceProvider.GetRequiredService<IInspectionAssignmentStore>();
        Assert.Empty((await assignments.TechniciansAsync(managerId, 1, 25, CancellationToken.None)).Technicians!.Items);
        Assert.Equal(InspectionAssignmentStatus.NotFound, (await AssignAsync(data.Factory, managerId, vehicleId, request)).Status);
        Assert.Equal(InspectionAssignmentStatus.NotFound, (await ReadAsync(data.Factory, managerId, inspectionId)).Status);
        await database.Users.Where(user => user.Id == managerId).ExecuteUpdateAsync(update => update.SetProperty(user => user.BranchId, data.BranchId));
        foreach (var role in new[] { AccountRoles.SalesAgent, AccountRoles.WorkshopTechnician, AccountRoles.GeneralManager, AccountRoles.Administrator })
        {
            Guid? assignment = role is AccountRoles.GeneralManager or AccountRoles.Administrator ? null : data.BranchId;
            await database.Users.Where(user => user.Id == managerId).ExecuteUpdateAsync(update =>
                update.SetProperty(user => user.Role, role).SetProperty(user => user.BranchId, assignment));
            Assert.Equal(InspectionAssignmentStatus.Forbidden, (await AssignAsync(data.Factory, managerId, vehicleId, request)).Status);
            Assert.Equal(InspectionAssignmentStatus.Forbidden,
                (await assignments.TechniciansAsync(managerId, 1, 25, CancellationToken.None)).Status);
        }
        await database.Users.Where(user => user.Id == managerId).ExecuteUpdateAsync(update =>
            update.SetProperty(user => user.Role, AccountRoles.BranchManager).SetProperty(user => user.BranchId, data.BranchId));
        await database.Users.Where(user => user.Id == technicianId).ExecuteUpdateAsync(update => update.SetProperty(user => user.IsActive, false));
        Assert.Equal(InspectionAssignmentStatus.Forbidden, (await ReadAsync(data.Factory, technicianId, inspectionId)).Status);
        await database.Users.Where(user => user.Id == technicianId).ExecuteUpdateAsync(update => update.SetProperty(user => user.IsActive, true));
        await database.Branches.Where(item => item.Id == data.BranchId).ExecuteUpdateAsync(update => update.SetProperty(item => item.IsActive, false));
        Assert.Equal(InspectionAssignmentStatus.Forbidden,
            (await assignments.TechniciansAsync(managerId, 1, 25, CancellationToken.None)).Status);
        Assert.Equal(InspectionAssignmentStatus.Forbidden, (await AssignAsync(data.Factory, managerId, vehicleId, request)).Status);
        Assert.Equal(InspectionAssignmentStatus.Forbidden, (await ReadAsync(data.Factory, technicianId, inspectionId)).Status);
    }

    private static async Task<(Guid Id, string Email)> ProvisionAsync(IServiceProvider services, Guid? branch, string role, string name)
    {
        var id = Guid.NewGuid();
        var email = $"{name}.{id:N}@example.invalid";
        await services.GetRequiredService<StaffProvisioningService>().ProvisionStaffAsync(
            new StaffProvisioningRequest(id, email, "Password123!", role, branch), CancellationToken.None);
        return (id, email);
    }

    private static async Task<InspectionAssignmentResult> AssignAsync(WebApplicationFactory<Program> factory, Guid actor,
        Guid vehicle, InspectionAssignmentRequest request)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<InspectionAssignmentService>()
            .AssignAsync(actor, vehicle, request, CancellationToken.None);
    }

    private static async Task<InspectionDraftResult> ReadAsync(WebApplicationFactory<Program> factory, Guid actor, Guid inspection)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IInspectionAssignmentStore>().ReadAsync(actor, inspection, CancellationToken.None);
    }
}

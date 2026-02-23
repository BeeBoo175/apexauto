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

public sealed class InspectionReopeningTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task ManagerReopeningRetainsRejectedHistoryAndRequiresFreshAttestationAcrossRacesAndRestart()
    {
        using var data = await CheckInTestData.CreateAsync(fixture);
        var appointment = await data.BookAsync();
        using var scope = data.Factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var checkIn = await services.GetRequiredService<AppointmentCheckInService>()
            .CheckInAsync(data.StaffId, appointment.Id, CheckInTestData.Evidence, CancellationToken.None);
        var (technician, _) = await ProvisionAsync(services, data.BranchId, AccountRoles.WorkshopTechnician);
        var (manager, email) = await ProvisionAsync(services, data.BranchId, AccountRoles.BranchManager);
        var (group, _) = await ProvisionAsync(services, null, AccountRoles.GeneralManager);
        var (admin, _) = await ProvisionAsync(services, null, AccountRoles.Administrator);
        var branch = Guid.NewGuid();
        await services.GetRequiredService<BranchProvisioningService>().ProvisionBranchAsync(
            DevelopmentFixtureManifest.Branches[1] with { Id = branch }, CancellationToken.None);
        var (outside, _) = await ProvisionAsync(services, branch, AccountRoles.BranchManager);
        var claim = await services.GetRequiredService<InspectionAssignmentService>().AssignAsync(technician,
            checkIn.Receipt!.VehicleId, new(Guid.NewGuid(), technician, 0, null), CancellationToken.None);
        var inspection = claim.Receipt!.InspectionId;
        var request = new InspectionReopenRequest(Guid.NewGuid(), 1, 1, "Manager requests verification of retained flood evidence");
        Assert.Equal(InspectionAssignmentStatus.Conflict, (await ReopenAsync(data.Factory, manager, inspection, request)).Status);
        var save = new InspectionDraftSaveRequest(Guid.NewGuid(), 1, 0,
            [new("FC-042", InspectionEntryKind.Finding, InspectionSeverity.CriticalDisqualifier, 0, "Cabin waterline evidence")])
        {
            ExaminedPointCodes = [.. InspectionCatalog.Points.Select(point => point.Code)]
        };
        var saves = services.GetRequiredService<IInspectionDraftSaveStore>();
        Assert.Equal(InspectionAssignmentStatus.Succeeded, (await saves.SaveAsync(technician, inspection, save, CancellationToken.None)).Status);
        var reports = services.GetRequiredService<IInspectionReportStore>();
        var original = (await reports.SignAsync(technician, inspection, new(Guid.NewGuid(), 1, 1, 1), CancellationToken.None)).Report!;
        Assert.True(original.Assessment.IsRejected);

        foreach (var actor in new[] { technician, group, admin, data.StaffId, data.OwnerId })
        {
            Assert.Equal(InspectionAssignmentStatus.Forbidden, (await ReopenAsync(data.Factory, actor, inspection, request)).Status);
        }
        Assert.Equal(InspectionAssignmentStatus.NotFound, (await ReopenAsync(data.Factory, outside, inspection, request)).Status);
        foreach (var reason in new string?[] { null, " ", "line\nbreak", new('a', 1001) })
        {
            Assert.Equal(InspectionAssignmentStatus.Invalid,
                (await ReopenAsync(data.Factory, manager, inspection, request with { Reason = reason })).Status);
        }
        Assert.Equal(InspectionAssignmentStatus.Conflict,
            (await ReopenAsync(data.Factory, manager, inspection, request with { AssignmentVersion = 2 })).Status);
        await InspectionReopeningSqlAssertions.VerifyBeforeAsync(fixture.Database, original, manager, technician, outside);

        await using var stale = new NpgsqlConnection(fixture.Database.RuntimeConnectionString);
        await stale.OpenAsync();
        await using var oldSnapshot = await stale.BeginTransactionAsync(IsolationLevel.RepeatableRead);
        await using (var read = new NpgsqlCommand("SELECT \"Revision\" FROM \"InspectionDrafts\" WHERE \"Id\" = @inspection", stale, oldSnapshot))
        {
            read.Parameters.AddWithValue("inspection", inspection);
            Assert.Equal(1L, await read.ExecuteScalarAsync());
        }
        var race = await Task.WhenAll(ReopenAsync(data.Factory, manager, inspection, request),
            ReopenAsync(data.Factory, manager, inspection, request with { OperationId = Guid.NewGuid() }));
        var receipt = Assert.Single(race, result => result.Status == InspectionAssignmentStatus.Succeeded).Receipt!;
        Assert.Single(race, result => result.Status == InspectionAssignmentStatus.Conflict);
        request = request with { OperationId = receipt.OperationId };
        await InspectionReopeningSqlAssertions.VerifyOldSnapshotAsync(stale, oldSnapshot, inspection);
        await oldSnapshot.RollbackAsync();
        await InspectionReopeningSqlAssertions.VerifyAfterAsync(fixture.Database, original, receipt);

        using var restarted = fixture.CreateFactory();
        var replay = await Task.WhenAll(ReopenAsync(restarted, manager, inspection, request), ReopenAsync(restarted, manager, inspection, request));
        Assert.All(replay, result => Assert.Equal(receipt, result.Receipt));
        Assert.Equal(InspectionAssignmentStatus.Conflict,
            (await ReopenAsync(restarted, manager, inspection, request with { Reason = "Changed request" })).Status);
        using var restoredScope = restarted.Services.CreateScope();
        var restored = restoredScope.ServiceProvider;
        var draft = (await restored.GetRequiredService<IInspectionAssignmentStore>()
            .ReadAsync(technician, inspection, CancellationToken.None)).Draft!;
        Assert.False(draft.IsSigned);
        Assert.Equal(2, draft.Revision);
        Assert.Equal(1, draft.ReopenedAfterSaveVersion);
        var restoredReports = restored.GetRequiredService<IInspectionReportStore>();
        Assert.Equal(InspectionAssignmentStatus.Conflict,
            (await restoredReports.SignAsync(technician, inspection, new(Guid.NewGuid(), 1, 1, 2), CancellationToken.None)).Status);
        save = save with { OperationId = Guid.NewGuid(), ExpectedVersion = 1, ExaminedPointCodes = null };
        var restoredSaves = restored.GetRequiredService<IInspectionDraftSaveStore>();
        Assert.Equal(InspectionAssignmentStatus.Succeeded, (await restoredSaves.SaveAsync(technician, inspection, save, CancellationToken.None)).Status);
        Assert.Equal(InspectionAssignmentStatus.Conflict,
            (await restoredReports.SignAsync(technician, inspection, new(Guid.NewGuid(), 1, 2, 2), CancellationToken.None)).Status);
        save = save with
        {
            OperationId = Guid.NewGuid(),
            ExpectedVersion = 2,
            Entries = [],
            ExaminedPointCodes = [.. InspectionCatalog.Points.Select(point => point.Code)]
        };
        Assert.Equal(InspectionAssignmentStatus.Succeeded, (await restoredSaves.SaveAsync(technician, inspection, save, CancellationToken.None)).Status);
        var second = (await restoredReports.SignAsync(technician, inspection, new(Guid.NewGuid(), 1, 3, 2), CancellationToken.None)).Report!;
        Assert.False(second.Assessment.IsRejected);
        Assert.Equal(InspectionGrade.A, second.Assessment.Grade);
        Assert.Equal(receipt, (await ReopenAsync(restarted, manager, inspection, request)).Receipt);
        Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize((await restoredReports
            .ReadAsync(group, inspection, 1, CancellationToken.None)).Report));
        await VerifyApiAsync(restarted, email, inspection, request, receipt);
        await restored.GetRequiredService<DealershipDbContext>().Users.Where(user => user.Id == manager)
            .ExecuteUpdateAsync(update => update.SetProperty(user => user.IsActive, false));
        Assert.Equal(InspectionAssignmentStatus.Forbidden, (await ReopenAsync(restarted, manager, inspection, request)).Status);
    }

    private static async Task VerifyApiAsync(WebApplicationFactory<Program> factory, string email, Guid inspection,
        InspectionReopenRequest request, InspectionReopenReceipt receipt)
    {
        using var client = factory.CreateClient();
        var path = $"/api/staff/inspection/drafts/{inspection}/reopen";
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(path, request)).StatusCode);
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(Guid.NewGuid(), email, "Password123!"));
        var session = (await login.Content.ReadFromJsonAsync<LoginSession>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        var replay = await client.PostAsJsonAsync(path, request);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.True(replay.Headers.CacheControl?.NoStore);
        Assert.Equal(receipt, await replay.Content.ReadFromJsonAsync<InspectionReopenReceipt>());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path, request with { Reason = null })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(path, request with { Reason = "Different reason" })).StatusCode);
    }

    private static async Task<(Guid Id, string Email)> ProvisionAsync(IServiceProvider services, Guid? branch, string role)
    {
        var id = Guid.NewGuid();
        var email = $"azlan.hakimi.{id:N}@example.invalid";
        await services.GetRequiredService<StaffProvisioningService>().ProvisionStaffAsync(
            new(id, email, "Password123!", role, branch), CancellationToken.None);
        return (id, email);
    }

    private static async Task<InspectionReopenResult> ReopenAsync(WebApplicationFactory<Program> factory, Guid actor,
        Guid inspection, InspectionReopenRequest request)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IInspectionReportStore>()
            .ReopenAsync(actor, inspection, request, CancellationToken.None);
    }
}

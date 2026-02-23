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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dealership.IntegrationTests;

public sealed class InspectionHistoryTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task HistoriesPageFrozenRevisionsAndLinkedRepairsWithinTheCurrentActorScope()
    {
        using var data = await CheckInTestData.CreateAsync(fixture);
        using var scope = data.Factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var (technician, email) = await ProvisionAsync(services, data.BranchId, AccountRoles.WorkshopTechnician);
        var (colleague, _) = await ProvisionAsync(services, data.BranchId, AccountRoles.WorkshopTechnician);
        var (manager, _) = await ProvisionAsync(services, data.BranchId, AccountRoles.BranchManager);
        var (group, _) = await ProvisionAsync(services, null, AccountRoles.GeneralManager);
        var (admin, _) = await ProvisionAsync(services, null, AccountRoles.Administrator);
        var branch = Guid.NewGuid();
        await services.GetRequiredService<BranchProvisioningService>().ProvisionBranchAsync(
            DevelopmentFixtureManifest.Branches[1] with { Id = branch }, CancellationToken.None);
        var (outside, _) = await ProvisionAsync(services, branch, AccountRoles.BranchManager);
        var inspection = await ClaimAsync(data, technician, 0, "WYY 8821", CheckInTestData.Evidence.Vin!);
        var histories = services.GetRequiredService<IInspectionHistoryStore>();
        Assert.Empty((await histories.RevisionsAsync(technician, inspection, new(), CancellationToken.None)).History!.Items);
        Assert.Equal(InspectionAssignmentStatus.NotFound,
            (await histories.RepairsAsync(technician, inspection, 1, new(), CancellationToken.None)).Status);
        var saves = services.GetRequiredService<InspectionDraftSaveService>();
        var reopenings = services.GetRequiredService<InspectionReopeningService>();
        var signoffs = services.GetRequiredService<InspectionSignoffService>();
        var repairRequests = services.GetRequiredService<InspectionRepairRequestService>();
        var approvals = services.GetRequiredService<IInspectionRepairApprovalStore>();
        var originals = new List<InspectionReport>();
        var requests = new List<InspectionRepairReceipt>();
        for (var revision = 1; revision <= 21; revision++)
        {
            var saved = await saves.SaveAsync(technician, inspection, Save(revision - 1), CancellationToken.None);
            Assert.Equal(InspectionAssignmentStatus.Succeeded, saved.Status);
            var signed = await signoffs.SignAsync(technician, inspection, new(Guid.NewGuid(), 1, revision, revision), CancellationToken.None);
            Assert.Equal(InspectionAssignmentStatus.Succeeded, signed.Status);
            originals.Add(signed.Report!);
            if (revision < 21)
            {
                Assert.Equal(InspectionAssignmentStatus.Succeeded, (await reopenings.ReopenAsync(manager, inspection,
                    new(Guid.NewGuid(), 1, revision, "Verify the retained bumper assessment"), CancellationToken.None)).Status);
            }
        }
        for (var version = 1; version <= 21; version++)
        {
            var requested = await repairRequests.RequestAsync(technician, inspection, new(Guid.NewGuid(), 1, 21, version - 1,
                [new("EP-004", "Prepare and refinish bumper surface")], 250m + version), CancellationToken.None);
            Assert.Equal(InspectionAssignmentStatus.Succeeded, requested.Status);
            requests.Add(requested.Receipt!);
            if (version % 2 == 0)
            {
                Assert.Equal(InspectionAssignmentStatus.Succeeded, (await approvals.ApproveAsync(manager, inspection,
                    new(Guid.NewGuid(), requested.Receipt!.OperationId, 1, 21, version), CancellationToken.None)).Status);
            }
        }

        // A separate assigned inspection detects cross-record leaks in both totals and returned rows.
        var unrelated = await ClaimAsync(data, colleague, 1, "VHP 7824", "PL1BT65R84B123457");
        await saves.SaveAsync(colleague, unrelated, Save(0), CancellationToken.None);
        await signoffs.SignAsync(colleague, unrelated, new(Guid.NewGuid(), 1, 1, 1), CancellationToken.None);
        await repairRequests.RequestAsync(colleague, unrelated, new(Guid.NewGuid(), 1, 1, 0,
            [new("EP-004", "Refinish bumper")], 100m), CancellationToken.None);
        var database = services.GetRequiredService<DealershipDbContext>();
        await database.Users.Where(user => user.Id == technician)
            .ExecuteUpdateAsync(update => update.SetProperty(user => user.UserName, "Amir Hakim Updated"));
        var revisionRows = new List<InspectionRevisionSummary>();
        var repairRows = new List<InspectionRepairHistoryEntry>();
        foreach (var page in new[] { 1, 2, 3 })
        {
            var revisions = (await histories.RevisionsAsync(technician, inspection, new(page, 10), CancellationToken.None)).History!;
            var work = (await histories.RepairsAsync(manager, inspection, 21, new(page, 10), CancellationToken.None)).History!;
            Assert.Equal(21, revisions.TotalCount);
            Assert.Equal(21, work.TotalCount);
            Assert.Equal(page == 3 ? 1 : 10, revisions.Items.Count);
            Assert.Equal(page == 3 ? 1 : 10, work.Items.Count);
            revisionRows.AddRange(revisions.Items);
            repairRows.AddRange(work.Items);
        }
        Assert.Equal(originals.AsEnumerable().Reverse().Select(item => item.OperationId), revisionRows.Select(item => item.OperationId));
        Assert.Equal(requests.AsEnumerable().Reverse().Select(item => item.OperationId), repairRows.Select(item => item.Request.OperationId));
        Assert.Equal(21, revisionRows.Select(item => item.OperationId).Distinct().Count());
        Assert.All(revisionRows, row =>
        {
            var original = originals.Single(item => item.Revision == row.Revision);
            Assert.Equal(original.TechnicianIdentity, row.TechnicianIdentity);
            Assert.Equal(original.Assessment, row.Assessment);
        });
        Assert.Null(revisionRows[0].Reopening);
        Assert.All(revisionRows.Skip(1), row => Assert.Equal(row.Revision + 1, row.Reopening!.Revision));
        Assert.All(repairRows, row => Assert.Equal(row.Request.Version % 2 == 0, row.Approval is not null));
        Assert.All(repairRows.Where(row => row.Approval is not null), row => Assert.Equal(row.Request.OperationId, row.Approval!.RequestId));
        foreach (var actor in new[] { technician, manager, group })
        {
            Assert.Equal(InspectionAssignmentStatus.Succeeded, (await histories.RevisionsAsync(actor, inspection, new(), CancellationToken.None)).Status);
            Assert.Equal(InspectionAssignmentStatus.Succeeded, (await histories.RepairsAsync(actor, inspection, 21, new(), CancellationToken.None)).Status);
        }
        foreach (var actor in new[] { colleague, outside })
        {
            Assert.Equal(InspectionAssignmentStatus.NotFound, (await histories.RevisionsAsync(actor, inspection, new(), CancellationToken.None)).Status);
            Assert.Equal(InspectionAssignmentStatus.NotFound, (await histories.RepairsAsync(actor, inspection, 21, new(), CancellationToken.None)).Status);
        }
        foreach (var actor in new[] { admin, data.OwnerId, data.StaffId })
        {
            Assert.Equal(InspectionAssignmentStatus.Forbidden, (await histories.RevisionsAsync(actor, inspection, new(), CancellationToken.None)).Status);
            Assert.Equal(InspectionAssignmentStatus.Forbidden, (await histories.RepairsAsync(actor, inspection, 21, new(), CancellationToken.None)).Status);
        }
        foreach (var query in new[] { new InspectionHistoryQuery(0), new InspectionHistoryQuery(1, 20) })
        {
            Assert.Equal(InspectionAssignmentStatus.Invalid, (await histories.RevisionsAsync(technician, inspection, query, CancellationToken.None)).Status);
            Assert.Equal(InspectionAssignmentStatus.Invalid, (await histories.RepairsAsync(technician, inspection, 21, query, CancellationToken.None)).Status);
        }
        Assert.Empty((await histories.RevisionsAsync(technician, inspection, new(int.MaxValue, 50), CancellationToken.None)).History!.Items);
        Assert.Empty((await histories.RepairsAsync(technician, inspection, 21, new(int.MaxValue, 50), CancellationToken.None)).History!.Items);
        Assert.Empty((await histories.RepairsAsync(technician, inspection, 1, new(), CancellationToken.None)).History!.Items);
        using var restarted = fixture.CreateFactory();
        using var restartedScope = restarted.Services.CreateScope();
        var restored = restartedScope.ServiceProvider.GetRequiredService<IInspectionHistoryStore>();
        Assert.Equal(JsonSerializer.Serialize((await histories.RevisionsAsync(group, inspection, new(), CancellationToken.None)).History),
            JsonSerializer.Serialize((await restored.RevisionsAsync(group, inspection, new(), CancellationToken.None)).History));
        Assert.Equal(JsonSerializer.Serialize((await histories.RepairsAsync(group, inspection, 21, new(), CancellationToken.None)).History),
            JsonSerializer.Serialize((await restored.RepairsAsync(group, inspection, 21, new(), CancellationToken.None)).History));
        using var client = restarted.CreateClient();
        var revisionPath = $"/api/staff/inspection/reports/{inspection}/revisions";
        var repairPath = $"{revisionPath}/21/repair-history";
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(revisionPath)).StatusCode);
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(Guid.NewGuid(), email, "Password123!"));
        var session = (await login.Content.ReadFromJsonAsync<LoginSession>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        var response = await client.GetAsync($"{revisionPath}?page=3&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Single((await response.Content.ReadFromJsonAsync<InspectionRevisionHistory>())!.Items);
        response = await client.GetAsync($"{repairPath}?page=3&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Single((await response.Content.ReadFromJsonAsync<InspectionRepairHistory>())!.Items);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"{repairPath}?pageSize=20")).StatusCode);
        await database.Users.Where(user => user.Id == technician).ExecuteUpdateAsync(update => update.SetProperty(user => user.IsActive, false));
        Assert.Equal(InspectionAssignmentStatus.Forbidden, (await histories.RevisionsAsync(technician, inspection, new(), CancellationToken.None)).Status);
        await database.Branches.Where(item => item.Id == data.BranchId).ExecuteUpdateAsync(update => update.SetProperty(item => item.IsActive, false));
        Assert.Equal(InspectionAssignmentStatus.Forbidden, (await histories.RepairsAsync(manager, inspection, 21, new(), CancellationToken.None)).Status);
        Assert.Equal(InspectionAssignmentStatus.Succeeded, (await histories.RevisionsAsync(group, inspection, new(), CancellationToken.None)).Status);
    }

    private static InspectionDraftSaveRequest Save(long expectedVersion) => new(Guid.NewGuid(), 1, expectedVersion,
        [new("EP-004", InspectionEntryKind.Finding, InspectionSeverity.MinorDefect, 250m + expectedVersion, "Shallow bumper scratch")])
    {
        ExaminedPointCodes = [.. InspectionCatalog.Points.Select(point => point.Code)]
    };

    private static async Task<Guid> ClaimAsync(CheckInTestData data, Guid technician, int slot, string plate, string vin)
    {
        var appointment = await data.BookAsync(slot, plate);
        using var scope = data.Factory.Services.CreateScope();
        var checkIn = await scope.ServiceProvider.GetRequiredService<AppointmentCheckInService>()
            .CheckInAsync(data.StaffId, appointment.Id, CheckInTestData.Evidence with { Vin = vin }, CancellationToken.None);
        Assert.Equal(CheckInStatus.Succeeded, checkIn.Status);
        var claim = await scope.ServiceProvider.GetRequiredService<InspectionAssignmentService>().AssignAsync(technician,
            checkIn.Receipt!.VehicleId, new(Guid.NewGuid(), technician, 0, null), CancellationToken.None);
        return claim.Receipt!.InspectionId;
    }

    private static async Task<(Guid Id, string Email)> ProvisionAsync(IServiceProvider services, Guid? branch, string role)
    {
        var id = Guid.NewGuid();
        var email = $"amir.hakim.{id:N}@example.invalid";
        await services.GetRequiredService<StaffProvisioningService>().ProvisionStaffAsync(new(id, email, "Password123!", role, branch), CancellationToken.None);
        return (id, email);
    }
}

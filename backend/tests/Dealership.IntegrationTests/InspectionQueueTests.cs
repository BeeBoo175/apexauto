using System.Data.Common;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Dealership.Application.Accounts;
using Dealership.Application.Inspections;
using Dealership.Application.Intake;
using Dealership.Domain.Inspections;
using Dealership.Infrastructure.Inspections;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Dealership.IntegrationTests;

public sealed class InspectionQueueTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task QueuesScopeCurrentWorkAndRetainedApprovalsWithCoherentSnapshotsAndBoundedHttpReads()
    {
        using var data = await CheckInTestData.CreateAsync(fixture);
        using var scope = data.Factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var database = services.GetRequiredService<DealershipDbContext>();
        var (technician, _) = await ProvisionAsync(services, data.BranchId, AccountRoles.WorkshopTechnician);
        var (colleague, _) = await ProvisionAsync(services, data.BranchId, AccountRoles.WorkshopTechnician);
        var (manager, email) = await ProvisionAsync(services, data.BranchId, AccountRoles.BranchManager);
        var (group, _) = await ProvisionAsync(services, null, AccountRoles.GeneralManager);
        var (admin, _) = await ProvisionAsync(services, null, AccountRoles.Administrator);
        var inspection = Guid.NewGuid();
        var vehicles = new List<Guid>();
        for (var index = 0; index < 3; index++)
        {
            var appointment = await data.BookAsync(index, $"WYY {8821 + index}");
            var checkedIn = await services.GetRequiredService<AppointmentCheckInService>().CheckInAsync(data.StaffId,
                appointment.Id, CheckInTestData.Evidence with { Vin = $"PL1BT65R84B12345{index}" }, CancellationToken.None);
            Assert.Equal(CheckInStatus.Succeeded, checkedIn.Status);
            vehicles.Add(checkedIn.Receipt!.VehicleId);
            if (index == 2)
            {
                continue;
            }
            var assigned = index == 0 ? technician : colleague;
            Assert.Equal(InspectionAssignmentStatus.Succeeded, (await services.GetRequiredService<InspectionAssignmentService>()
                .AssignAsync(assigned, vehicles[index], new(index == 0 ? inspection : Guid.NewGuid(), assigned, 0, null), CancellationToken.None)).Status);
        }

        var queues = services.GetRequiredService<IInspectionQueueStore>();
        var own = (await queues.InspectionsAsync(technician, new(), InspectionQueueStatus.All, CancellationToken.None)).Queue!;
        Assert.Equal(2, own.Total);
        var expected = await database.Vehicles.AsNoTracking().Where(item => item.Id != vehicles[1])
            .OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id).Select(item => item.Id).ToArrayAsync();
        Assert.Equal(expected, own.Items.Select(item => item.VehicleId));
        Assert.Equal(vehicles[2], Assert.Single(own.Items, item => item.Status == InspectionQueueStatus.Unassigned).VehicleId);
        Assert.Equal(vehicles[0], Assert.Single(own.Items, item => item.Status == InspectionQueueStatus.Draft).VehicleId);
        Assert.Equal(3, (await queues.InspectionsAsync(manager, new(), InspectionQueueStatus.All, CancellationToken.None)).Queue!.Total);
        Assert.Equal(3, (await queues.InspectionsAsync(group, new(), InspectionQueueStatus.All, CancellationToken.None)).Queue!.Total);
        Assert.Equal(2, (await queues.InspectionsAsync(manager, new(), InspectionQueueStatus.Draft, CancellationToken.None)).Queue!.Total);
        foreach (var actor in new[] { data.StaffId, data.OwnerId, admin, Guid.NewGuid() })
        {
            Assert.Equal(InspectionAssignmentStatus.Forbidden, (await queues.InspectionsAsync(actor, new(), InspectionQueueStatus.All, CancellationToken.None)).Status);
            Assert.Equal(InspectionAssignmentStatus.Forbidden, (await queues.RepairsAsync(actor, new(), InspectionRepairQueueStatus.All, CancellationToken.None)).Status);
        }
        Assert.Equal(InspectionAssignmentStatus.Forbidden, (await queues.InspectionsAsync(manager,
            new(BranchId: Guid.NewGuid()), InspectionQueueStatus.All, CancellationToken.None)).Status);
        Assert.Empty((await queues.InspectionsAsync(group, new(BranchId: Guid.NewGuid()), InspectionQueueStatus.All, CancellationToken.None)).Queue!.Items);
        foreach (var term in new[] { "%", "_", "!" })
        {
            Assert.Empty((await queues.InspectionsAsync(manager, new(Search: term), InspectionQueueStatus.All, CancellationToken.None)).Queue!.Items);
        }
        Assert.Equal(vehicles[0], Assert.Single((await queues.InspectionsAsync(manager,
            new(Search: "wyy 8821"), InspectionQueueStatus.All, CancellationToken.None)).Queue!.Items).VehicleId);
        Assert.Equal(vehicles[0], Assert.Single((await queues.InspectionsAsync(manager,
            new(Search: vehicles[0].ToString()), InspectionQueueStatus.All, CancellationToken.None)).Queue!.Items).VehicleId);

        var save = new InspectionDraftSaveRequest(Guid.NewGuid(), 1, 0,
            [new("EP-004", InspectionEntryKind.Finding, InspectionSeverity.MinorDefect, 250m, "Shallow bumper scratch")])
        {
            ExaminedPointCodes = [.. InspectionCatalog.Points.Select(point => point.Code)]
        };
        Assert.Equal(InspectionAssignmentStatus.Succeeded, (await services.GetRequiredService<InspectionDraftSaveService>()
            .SaveAsync(technician, inspection, save, CancellationToken.None)).Status);
        Assert.Equal(InspectionAssignmentStatus.Succeeded, (await services.GetRequiredService<InspectionSignoffService>()
            .SignAsync(technician, inspection, new(Guid.NewGuid(), 1, 1, 1), CancellationToken.None)).Status);
        Assert.Single((await queues.InspectionsAsync(technician, new(), InspectionQueueStatus.Sealed, CancellationToken.None)).Queue!.Items);
        var repairs = services.GetRequiredService<IInspectionRepairRequestStore>();
        var request = new InspectionRepairRequest(Guid.NewGuid(), 1, 1, 0, [new("EP-004", "Refinish bumper")], 250m);
        var receipt = (await repairs.RequestAsync(technician, inspection, request, CancellationToken.None)).Receipt!;
        Assert.Single((await queues.RepairsAsync(manager, new(), InspectionRepairQueueStatus.PendingApproval, CancellationToken.None)).Queue!.Items);
        Assert.Empty((await queues.RepairsAsync(colleague, new(), InspectionRepairQueueStatus.All, CancellationToken.None)).Queue!.Items);
        var dates = new InspectionQueueFilter(From: receipt.RequestedAt, Until: receipt.RequestedAt.AddSeconds(1));
        Assert.Single((await queues.RepairsAsync(technician, dates, InspectionRepairQueueStatus.All, CancellationToken.None)).Queue!.Items);
        Assert.Empty((await queues.InspectionsAsync(technician, dates, InspectionQueueStatus.All, CancellationToken.None)).Queue!.Items);
        Assert.Empty((await queues.RepairsAsync(manager, dates with { From = receipt.RequestedAt.AddSeconds(-1), Until = receipt.RequestedAt },
            InspectionRepairQueueStatus.All, CancellationToken.None)).Queue!.Items);
        foreach (var term in new[] { "%", "_", "!" })
        {
            Assert.Empty((await queues.RepairsAsync(manager, new(Search: term), InspectionRepairQueueStatus.All, CancellationToken.None)).Queue!.Items);
        }
        Assert.Equal(receipt.OperationId, Assert.Single((await queues.RepairsAsync(manager, new(Search: inspection.ToString()),
            InspectionRepairQueueStatus.All, CancellationToken.None)).Queue!.Items).RequestId);

        var interceptor = new ApprovalAfterCount(async () =>
        {
            using var writer = data.Factory.Services.CreateScope();
            Assert.Equal(InspectionAssignmentStatus.Succeeded, (await writer.ServiceProvider.GetRequiredService<IInspectionRepairApprovalStore>()
                .ApproveAsync(manager, inspection, new(Guid.NewGuid(), receipt.OperationId, 1, 1, 1), CancellationToken.None)).Status);
        });
        var options = new DbContextOptionsBuilder<DealershipDbContext>().UseNpgsql(fixture.Database.RuntimeConnectionString)
            .AddInterceptors(interceptor).Options;
        await using (var reader = new DealershipDbContext(options))
        {
            var snapshot = (await new InspectionQueueStore(reader).RepairsAsync(manager, new(),
                InspectionRepairQueueStatus.PendingApproval, CancellationToken.None)).Queue!;
            Assert.True(interceptor.Invoked);
            Assert.Equal(1, snapshot.Total);
            Assert.Equal(InspectionRepairQueueStatus.PendingApproval, Assert.Single(snapshot.Items).Status);
        }
        Assert.Empty((await queues.RepairsAsync(manager, new(), InspectionRepairQueueStatus.PendingApproval, CancellationToken.None)).Queue!.Items);
        var approved = Assert.Single((await queues.RepairsAsync(manager, new(), InspectionRepairQueueStatus.Approved, CancellationToken.None)).Queue!.Items);
        Assert.NotNull(approved.ApprovalId);
        Assert.Equal(250m, approved.Budget);
        Assert.Equal(InspectionAssignmentStatus.Succeeded, (await repairs.RequestAsync(technician, inspection,
            request with { OperationId = Guid.NewGuid(), ExpectedVersion = 1, Budget = 200 }, CancellationToken.None)).Status);
        var reduced = Assert.Single((await queues.RepairsAsync(manager, new(), InspectionRepairQueueStatus.Approved, CancellationToken.None)).Queue!.Items);
        Assert.Null(reduced.ApprovalId);
        Assert.Equal(approved.ApprovalId, reduced.CoveringApprovalId);
        Assert.Empty((await queues.RepairsAsync(manager, new(), InspectionRepairQueueStatus.PendingApproval, CancellationToken.None)).Queue!.Items);
        var old = Assert.Single((await queues.RepairsAsync(manager, new(), InspectionRepairQueueStatus.Superseded, CancellationToken.None)).Queue!.Items);
        Assert.Equal(approved.ApprovalId, old.ApprovalId);
        Assert.Equal(InspectionAssignmentStatus.Succeeded, (await repairs.RequestAsync(technician, inspection,
            request with { OperationId = Guid.NewGuid(), ExpectedVersion = 2, Budget = 275 }, CancellationToken.None)).Status);
        Assert.Single((await queues.RepairsAsync(manager, new(), InspectionRepairQueueStatus.PendingApproval, CancellationToken.None)).Queue!.Items);
        Assert.Equal(InspectionAssignmentStatus.Succeeded, (await repairs.RequestAsync(technician, inspection,
            request with { OperationId = Guid.NewGuid(), ExpectedVersion = 3, Budget = 200, Items = [new("EP-004", "Replace bumper")] }, CancellationToken.None)).Status);
        Assert.Single((await queues.RepairsAsync(manager, new(), InspectionRepairQueueStatus.PendingApproval, CancellationToken.None)).Queue!.Items);
        Assert.Equal(InspectionAssignmentStatus.Succeeded, (await repairs.RequestAsync(technician, inspection,
            request with { OperationId = Guid.NewGuid(), ExpectedVersion = 4, Budget = 0 }, CancellationToken.None)).Status);
        Assert.Single((await queues.RepairsAsync(manager, new(), InspectionRepairQueueStatus.ApprovalNotRequired, CancellationToken.None)).Queue!.Items);
        Assert.Equal(InspectionAssignmentStatus.Succeeded, (await services.GetRequiredService<InspectionReopeningService>()
            .ReopenAsync(manager, inspection, new(Guid.NewGuid(), 1, 1, "Recheck bumper condition"), CancellationToken.None)).Status);
        Assert.Empty((await queues.RepairsAsync(manager, new(), InspectionRepairQueueStatus.ApprovalNotRequired, CancellationToken.None)).Queue!.Items);
        Assert.Equal(5, (await queues.RepairsAsync(manager, new(), InspectionRepairQueueStatus.Superseded, CancellationToken.None)).Queue!.Total);
        Assert.Equal(InspectionAssignmentStatus.Succeeded, (await services.GetRequiredService<InspectionDraftSaveService>()
            .SaveAsync(technician, inspection, save with { OperationId = Guid.NewGuid(), ExpectedVersion = 1 }, CancellationToken.None)).Status);
        Assert.Equal(InspectionAssignmentStatus.Succeeded, (await services.GetRequiredService<InspectionSignoffService>()
            .SignAsync(technician, inspection, new(Guid.NewGuid(), 1, 2, 2), CancellationToken.None)).Status);
        Assert.Equal(InspectionAssignmentStatus.Succeeded, (await repairs.RequestAsync(technician, inspection,
            request with { OperationId = Guid.NewGuid(), ExpectedVersion = 5, Revision = 2, Budget = 200 }, CancellationToken.None)).Status);
        var fresh = Assert.Single((await queues.RepairsAsync(manager, new(), InspectionRepairQueueStatus.PendingApproval, CancellationToken.None)).Queue!.Items);
        Assert.Null(fresh.CoveringApprovalId);

        using var restarted = fixture.CreateFactory();
        using var restartedScope = restarted.Services.CreateScope();
        var restored = restartedScope.ServiceProvider.GetRequiredService<IInspectionQueueStore>();
        Assert.Equal(JsonSerializer.Serialize((await queues.RepairsAsync(group, new(), InspectionRepairQueueStatus.All, CancellationToken.None)).Queue),
            JsonSerializer.Serialize((await restored.RepairsAsync(group, new(), InspectionRepairQueueStatus.All, CancellationToken.None)).Queue));
        await VerifyHttpAsync(restarted.CreateClient(), email);
        Assert.Equal(InspectionAssignmentStatus.Invalid, (await queues.InspectionsAsync(manager, new(PageSize: 20), InspectionQueueStatus.All, CancellationToken.None)).Status);
        Assert.Equal(InspectionAssignmentStatus.Invalid, (await queues.RepairsAsync(manager, new(), (InspectionRepairQueueStatus)999, CancellationToken.None)).Status);
        await database.Users.Where(item => item.Id == manager).ExecuteUpdateAsync(update => update.SetProperty(item => item.Role, AccountRoles.SalesAgent));
        Assert.Equal(InspectionAssignmentStatus.Forbidden, (await queues.RepairsAsync(manager, new(), InspectionRepairQueueStatus.All, CancellationToken.None)).Status);
        await database.Users.Where(item => item.Id == technician).ExecuteUpdateAsync(update => update.SetProperty(item => item.IsActive, false));
        Assert.Equal(InspectionAssignmentStatus.Forbidden, (await queues.InspectionsAsync(technician, new(), InspectionQueueStatus.All, CancellationToken.None)).Status);
        await database.Branches.Where(item => item.Id == data.BranchId).ExecuteUpdateAsync(update => update.SetProperty(item => item.IsActive, false));
        Assert.Equal(InspectionAssignmentStatus.Forbidden, (await queues.RepairsAsync(colleague, new(), InspectionRepairQueueStatus.All, CancellationToken.None)).Status);
        Assert.Equal(3, (await queues.InspectionsAsync(group, new(), InspectionQueueStatus.All, CancellationToken.None)).Queue!.Total);
    }

    private static async Task VerifyHttpAsync(HttpClient client, string email)
    {
        using (client)
        {
            const string vehicles = "/api/staff/inspection/vehicles";
            const string repairs = "/api/staff/inspection/repair-requests";
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(vehicles)).StatusCode);
            var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(Guid.NewGuid(), email, "Password123!"));
            var session = (await login.Content.ReadFromJsonAsync<LoginSession>())!;
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
            var response = await client.GetAsync(vehicles);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(response.Headers.CacheControl?.NoStore);
            Assert.Equal(3, (await response.Content.ReadFromJsonAsync<InspectionQueuePage>())!.Total);
            response = await client.GetAsync(repairs);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(response.Headers.CacheControl?.NoStore);
            Assert.Single((await response.Content.ReadFromJsonAsync<InspectionRepairQueuePage>())!.Items);
            Assert.Equal(5, (await client.GetFromJsonAsync<InspectionRepairQueuePage>($"{repairs}?status=Superseded"))!.Total);
            foreach (var path in new[] { vehicles, repairs })
            {
                foreach (var query in new[] { "pageSize=20", "page=0", "status=999", "status=0", "from=2026-10-22" })
                {
                    Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"{path}?{query}")).StatusCode);
                }
            }
        }
    }

    private static async Task<(Guid Id, string Email)> ProvisionAsync(IServiceProvider services, Guid? branch, string role)
    {
        var id = Guid.NewGuid();
        var email = $"amir.hakim.{id:N}@example.invalid";
        await services.GetRequiredService<StaffProvisioningService>().ProvisionStaffAsync(new(id, email, "Password123!", role, branch), CancellationToken.None);
        return (id, email);
    }

    private sealed class ApprovalAfterCount(Func<Task> approve) : DbCommandInterceptor
    {
        public bool Invoked { get; private set; }

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command,
            CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (!Invoked && command.CommandText.Contains("SELECT count(*)", StringComparison.Ordinal) &&
                command.CommandText.Contains("InspectionRepairRequests", StringComparison.Ordinal))
            {
                Invoked = true;
                await approve();
            }
            return result;
        }
    }
}

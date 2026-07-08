using Dealership.Application.Accounts;
using Dealership.Application.Inspections;
using Dealership.Application.Sales;
using Dealership.Application.WorkCalendar;
using Dealership.Domain.Sales;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Development;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dealership.IntegrationTests;

internal static class DevelopmentUnscheduledVolumeAssertions
{
    internal static async Task SeedInterruptedSaleAsync(IServiceProvider services)
    {
        var mail = services.GetRequiredService<DevelopmentFixtureMail>();
        await mail.CaptureAsync(CancellationToken.None);
        var scenario = DevelopmentSaleVolumeManifest.All[0];
        var customer = DevelopmentTestDriveVolumeManifest.All.First(item => item.Queue == "MyWork" &&
            item.Customer.Registration.RegistrationKey == scenario.Hold.RegistrationKey).Customer;
        var provisioned = await services.GetRequiredService<DevelopmentCustomerFixtures>()
            .SeedAsync(customer, CancellationToken.None);
        Assert.False(provisioned.RequiresAttention);
        var hold = await services.GetRequiredService<DevelopmentReservationFixtures>()
            .RunOneAsync(true, scenario.Hold, DateTimeOffset.UtcNow, CancellationToken.None);
        Assert.False(hold.RequiresAttention);
        var claim = await services.GetRequiredService<SaleHandlingService>().ClaimAsync(scenario.AgentId,
            scenario.ReservationId, new(scenario.OperationId("claim"), 1), CancellationToken.None);
        Assert.Equal(SaleResultStatus.Succeeded, claim.Status);
        var database = services.GetRequiredService<DealershipDbContext>();
        var partial = await database.SalesOrders.AsNoTracking().SingleAsync(item => item.Id == scenario.ReservationId);
        Assert.Equal(SalesOrderStatus.Handling, partial.Status);
        Assert.Equal(2, partial.Version);
        await mail.CompleteAsync(CancellationToken.None);
    }

    internal static async Task VerifyAsync(IServiceProvider services)
    {
        var database = services.GetRequiredService<DealershipDbContext>();
        var calendar = services.GetRequiredService<IWorkCalendarStore>();
        var generalManager = DevelopmentFixtureManifest.Staff.Single(item => item.Role == AccountRoles.GeneralManager);
        foreach (var technician in DevelopmentFixtureManifest.ReliefTechnicians)
        {
            var scenarios = DevelopmentIntakeManifest.WorkshopExamples.Where(item => item.BranchId == technician.BranchId)
                .OrderBy(item => item.RegistrationPlate, StringComparer.Ordinal).ToArray();
            var ids = scenarios.Select(item => item.Id).ToArray();
            var vehicles = await database.Vehicles.AsNoTracking().Where(item => ids.Contains(item.IntakeAppointmentId))
                .ToDictionaryAsync(item => item.IntakeAppointmentId, item => item.Id);
            var expected = scenarios.Select(item => vehicles[item.Id]).ToArray();
            Assert.Equal(DevelopmentIntakeManifest.WorkshopExamplesPerBranch, expected.Length);
            var manager = DevelopmentFixtureManifest.Staff.Single(item => item.Role == AccountRoles.BranchManager &&
                item.BranchId == technician.BranchId);
            var own = new UnscheduledWorkQuery(new(technician.BranchId), 1, 25);
            Assert.Equal(WorkCalendarReadStatus.Forbidden,
                (await calendar.UnscheduledAsync(technician.Id, own, CancellationToken.None)).Status);
            var ordered = await database.Vehicles.AsNoTracking().Where(item => expected.Contains(item.Id))
                .OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id)
                .Select(item => item.Id).ToArrayAsync();
            await AssertWorkshopPagesAsync(services.GetRequiredService<IWorkshopQueueStore>(), technician.Id, ordered);
            var oversight = own with { Scope = own.Scope with { Employee = technician.Email.ToUpperInvariant() } };
            await AssertPagesAsync(calendar, manager.Id, oversight, expected, technician.Id, ids);
            await AssertPagesAsync(calendar, generalManager.Id, oversight, expected, technician.Id, ids);
            var unrelated = await calendar.UnscheduledAsync(technician.Id,
                own with { Scope = new(DevelopmentFixtureManifest.Branches.First(item => item.Id != technician.BranchId).Id) },
                CancellationToken.None);
            Assert.Equal(WorkCalendarReadStatus.Forbidden, unrelated.Status);
            var unassigned = await calendar.UnscheduledAsync(manager.Id,
                own with { Scope = own.Scope with { Employee = "unassigned" } }, CancellationToken.None);
            Assert.Empty(unassigned.Value!.Items);
            Assert.Equal(0, unassigned.Value.Total);
        }

        await VerifySalesAndMixedPagesAsync(database, calendar, generalManager.Id);
    }

    private static async Task VerifySalesAndMixedPagesAsync(DealershipDbContext database,
        IWorkCalendarStore calendar, Guid generalManagerId)
    {
        var scenarios = DevelopmentSaleScenarios.All.Concat(DevelopmentSaleVolumeManifest.All).ToArray();
        foreach (var branch in DevelopmentFixtureManifest.Branches)
        {
            var manager = DevelopmentFixtureManifest.Staff.Single(item => item.Role == AccountRoles.BranchManager &&
                item.BranchId == branch.Id);
            var branchSales = scenarios.Where(item => item.Hold.Listing.Intake.BranchId == branch.Id).ToArray();
            var query = new UnscheduledWorkQuery(new(branch.Id), 1, 25);
            var agents = new[]
            {
                DevelopmentFixtureManifest.PrimaryAgent(branch.Id),
                DevelopmentFixtureManifest.AdditionalAgents.Single(item => item.BranchId == branch.Id)
            };
            foreach (var agent in agents)
            {
                var own = branchSales.Where(item => item.AgentId == agent.Id).ToArray();
                var actionable = own.Where(item =>
                    item.Target is SalesOrderStatus.Handling or SalesOrderStatus.Approved or SalesOrderStatus.Settled)
                    .Select(item => new ExpectedWork(item.ReservationId, "Sale", item.Hold.Listing.Intake.RegistrationPlate))
                    .OrderBy(item => item.Registration, StringComparer.Ordinal).ToArray();
                Assert.NotEmpty(actionable);
                // A suspended agent cannot read their own queue; the manager view below still covers their work.
                if (agent.Id != DevelopmentLifecycleManifest.SuspendedStaffId)
                {
                    await AssertMixedPagesAsync(calendar, agent.Id, query, actionable, agent.Id);
                }

                await AssertMixedPagesAsync(calendar, manager.Id,
                    query with { Scope = query.Scope with { Employee = agent.Email.ToUpperInvariant() } },
                    [.. own.Where(item => item.Target is SalesOrderStatus.Handling or SalesOrderStatus.PendingApproval or
                            SalesOrderStatus.Approved or SalesOrderStatus.Settled)
                        .Select(item => new ExpectedWork(item.ReservationId, "Sale", item.Hold.Listing.Intake.RegistrationPlate))
                        .OrderBy(item => item.Registration, StringComparer.Ordinal)], agent.Id);
            }

            var vehicles = await database.Vehicles.AsNoTracking().Where(item => item.BranchId == branch.Id &&
                item.Status == VehicleStatus.PendingInspection).ToArrayAsync();
            var vehicleIds = vehicles.Select(item => item.Id).ToArray();
            var assigned = await database.InspectionDrafts.AsNoTracking().Where(item =>
                vehicleIds.Contains(item.VehicleId)).Select(item => item.VehicleId).ToArrayAsync();
            var workshop = vehicles.Where(item => assigned.Contains(item.Id))
                .Select(item => new ExpectedWork(item.Id, "Workshop", item.RegistrationPlate));
            var sales = scenarios.Where(item => item.Hold.Listing.Intake.BranchId == branch.Id &&
                item.Target is SalesOrderStatus.Handling or SalesOrderStatus.PendingApproval or
                    SalesOrderStatus.Approved or SalesOrderStatus.Settled)
                .Select(item => new ExpectedWork(item.ReservationId, "Sale", item.Hold.Listing.Intake.RegistrationPlate));
            var mixed = workshop.Concat(sales).OrderBy(item => item.Registration, StringComparer.Ordinal)
                .ThenBy(item => item.Kind, StringComparer.Ordinal).ThenBy(item => item.Id).ToArray();
            Assert.Contains(mixed, item => item.Kind == "Workshop");
            Assert.Contains(mixed, item => item.Kind == "Sale");
            await AssertMixedPagesAsync(calendar, manager.Id, query, mixed);
            await AssertMixedPagesAsync(calendar, generalManagerId, query, mixed);
        }
    }

    private sealed record ExpectedWork(Guid Id, string Kind, string Registration);

    private static async Task AssertWorkshopPagesAsync(IWorkshopQueueStore workshop, Guid technicianId, Guid[] expected)
    {
        var actual = new List<Guid>();
        for (var page = 1; page <= 4; page++)
        {
            var result = await workshop.ReadJobsAsync(technicianId, new InspectionQueueFilter(Page: page),
                WorkshopTask.MyWork, CancellationToken.None);
            Assert.Equal(InspectionAssignmentStatus.Succeeded, result.Status);
            var snapshot = Assert.IsType<WorkshopJobs>(result.Queue);
            Assert.Equal(expected.Length, snapshot.Total);
            Assert.Equal(page, snapshot.Page);
            Assert.Equal(expected.Skip((page - 1) * 25).Take(25), snapshot.Items.Select(item => item.VehicleId));
            actual.AddRange(snapshot.Items.Select(item => item.VehicleId));
        }

        Assert.Equal(expected, actual);
    }

    private static async Task AssertMixedPagesAsync(IWorkCalendarStore calendar, Guid actorId,
        UnscheduledWorkQuery query, ExpectedWork[] expected, Guid? handlerId = null)
    {
        var actual = new List<Guid>();
        for (var page = 1; page <= (expected.Length + 24) / 25 + 1; page++)
        {
            var result = await calendar.UnscheduledAsync(actorId, query with { Page = page }, CancellationToken.None);
            Assert.Equal(WorkCalendarReadStatus.Succeeded, result.Status);
            var snapshot = Assert.IsType<UnscheduledWorkPage>(result.Value);
            Assert.Equal(expected.Length, snapshot.Total);
            var slice = expected.Skip((page - 1) * 25).Take(25).ToArray();
            Assert.Equal(slice.Select(item => item.Id), snapshot.Items.Select(item => item.Id));
            Assert.Equal(slice.Select(item => item.Kind), snapshot.Items.Select(item => item.Kind));
            Assert.All(snapshot.Items, item =>
            {
                Assert.Equal(query.Scope.BranchId, item.BranchId);
                if (handlerId.HasValue)
                {
                    Assert.Equal(handlerId.Value, item.HandlerId);
                    Assert.Equal("Sale", item.Kind);
                }
            });
            actual.AddRange(snapshot.Items.Select(item => item.Id));
        }

        Assert.Equal(expected.Select(item => item.Id), actual);
        var refreshed = await calendar.UnscheduledAsync(actorId, query, CancellationToken.None);
        Assert.Equal(expected.Take(25).Select(item => item.Id), refreshed.Value!.Items.Select(item => item.Id));
        var empty = await calendar.UnscheduledAsync(actorId, query with { Page = int.MaxValue }, CancellationToken.None);
        Assert.Equal(expected.Length, empty.Value!.Total);
        Assert.Empty(empty.Value.Items);
    }

    // The technician's open drafts come first and in order; repair requests awaiting approval for the same technician
    // may follow, since signed history can leave a request with whoever signed it.
    private static async Task AssertPagesAsync(IWorkCalendarStore calendar, Guid actorId, UnscheduledWorkQuery query,
        Guid[] expected, Guid technicianId, Guid[] inspectionIds)
    {
        var all = new List<UnscheduledWorkItem>();
        var total = 0;
        for (var page = 1; all.Count < total || page == 1; page++)
        {
            var result = await calendar.UnscheduledAsync(actorId, query with { Page = page }, CancellationToken.None);
            Assert.Equal(WorkCalendarReadStatus.Succeeded, result.Status);
            var snapshot = Assert.IsType<UnscheduledWorkPage>(result.Value);
            total = snapshot.Total;
            Assert.Equal(Math.Min(25, total - (page - 1) * 25), snapshot.Items.Count);
            all.AddRange(snapshot.Items);
        }

        var drafts = all.Where(item => item.Status == "InspectionUnderway").ToArray();
        Assert.Equal(expected, drafts.Select(item => item.Id));
        Assert.All(drafts, item =>
        {
            Assert.Equal("Workshop", item.Kind);
            Assert.Equal(technicianId, item.HandlerId);
            Assert.Contains(item.InspectionId!.Value, inspectionIds);
        });
        Assert.All(all.Where(item => item.Status != "InspectionUnderway"), item =>
        {
            Assert.Equal("Workshop", item.Kind);
            Assert.Equal("ApprovalRequired", item.Status);
            Assert.Equal(technicianId, item.HandlerId);
        });
        var outside = await calendar.UnscheduledAsync(actorId, query with { Page = int.MaxValue }, CancellationToken.None);
        Assert.Empty(outside.Value!.Items);
        Assert.Equal(all.Count, outside.Value.Total);
    }
}

using Dealership.Application.Accounts;
using Dealership.Application.WorkCalendar;
using Dealership.Infrastructure.Development;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dealership.IntegrationTests;

internal static class DevelopmentUnscheduledVolumeAssertions
{
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
            Assert.Equal(51, expected.Length);
            var manager = DevelopmentFixtureManifest.Staff.Single(item => item.Role == AccountRoles.BranchManager &&
                item.BranchId == technician.BranchId);
            var own = new UnscheduledWorkQuery(new(technician.BranchId), 1, 25);
            await AssertPagesAsync(calendar, technician.Id, own, expected, technician.Id, ids);
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
    }

    private static async Task AssertPagesAsync(IWorkCalendarStore calendar, Guid actorId, UnscheduledWorkQuery query,
        Guid[] expected, Guid technicianId, Guid[] inspectionIds)
    {
        var actual = new List<Guid>();
        for (var page = 1; page <= 4; page++)
        {
            var result = await calendar.UnscheduledAsync(actorId, query with { Page = page }, CancellationToken.None);
            Assert.Equal(WorkCalendarReadStatus.Succeeded, result.Status);
            var snapshot = Assert.IsType<UnscheduledWorkPage>(result.Value);
            Assert.Equal(51, snapshot.Total);
            Assert.Equal(expected.Skip((page - 1) * 25).Take(25), snapshot.Items.Select(item => item.Id));
            Assert.All(snapshot.Items, item =>
            {
                Assert.Equal("Workshop", item.Kind);
                Assert.Equal("InspectionUnderway", item.Status);
                Assert.Equal(technicianId, item.HandlerId);
                Assert.Contains(item.InspectionId!.Value, inspectionIds);
            });
            actual.AddRange(snapshot.Items.Select(item => item.Id));
        }

        Assert.Equal(expected, actual);
        var refreshed = await calendar.UnscheduledAsync(actorId, query, CancellationToken.None);
        Assert.Equal(expected.Take(25), refreshed.Value!.Items.Select(item => item.Id));
        var outside = await calendar.UnscheduledAsync(actorId, query with { Page = int.MaxValue }, CancellationToken.None);
        Assert.Empty(outside.Value!.Items);
        Assert.Equal(51, outside.Value.Total);
    }
}

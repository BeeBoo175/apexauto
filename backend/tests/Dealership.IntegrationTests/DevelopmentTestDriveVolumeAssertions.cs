using Dealership.Domain.Common;
using System.Globalization;
using Dealership.Application.Accounts;
using Dealership.Application.TestDrives;
using Dealership.Application.WorkCalendar;
using Dealership.Domain.Intake;
using Dealership.Infrastructure.Development;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dealership.IntegrationTests;

internal static class DevelopmentTestDriveVolumeAssertions
{
    internal static async Task VerifyAsync(IServiceProvider services)
    {
        var database = services.GetRequiredService<DealershipDbContext>();
        var store = services.GetRequiredService<IStaffTestDriveReadStore>();
        var calendar = services.GetRequiredService<IWorkCalendarStore>();
        var generalManager = DevelopmentFixtureManifest.Staff.Single(item => item.Role == AccountRoles.GeneralManager);
        foreach (var branch in DevelopmentFixtureManifest.Branches)
        {
            var manager = DevelopmentFixtureManifest.Staff.Single(item => item.BranchId == branch.Id &&
                item.Role == AccountRoles.BranchManager);
            // Volume bookings are spread over the horizon: no branch day holds a queue, and many days are used.
            var volumeBookingIds = DevelopmentTestDriveVolumeManifest.All.Where(item => item.BranchId == branch.Id)
                .Select(item => item.OperationId("booking")).ToArray();
            var starts = await database.TestDriveBookings.AsNoTracking().Where(item => volumeBookingIds.Contains(item.Id))
                .Select(item => item.StartsAt).ToArrayAsync();
            var perDay = starts.GroupBy(item => DateOnly.FromDateTime(
                TimeZoneInfo.ConvertTime(item, DealershipLocale.Malaysia).DateTime)).Select(group => group.Count()).ToArray();
            Assert.True(perDay.Max() <= 8, $"{branch.Name} holds {perDay.Max()} volume drives on one day");
            Assert.True(perDay.Length >= 10, $"{branch.Name} uses only {perDay.Length} days");
            var agents = new[]
            {
                DevelopmentFixtureManifest.PrimaryAgent(branch.Id),
                DevelopmentFixtureManifest.AdditionalAgents.Single(item => item.BranchId == branch.Id)
            };
            foreach (var agent in agents)
            {
                foreach (var queue in new[] { "ToClaim", "MyWork", "History" })
                {
                    var scenarios = DevelopmentTestDriveVolumeManifest.All.Where(item => item.BranchId == branch.Id &&
                        item.Queue == queue && (queue == "ToClaim" || item.HandlerId == agent.Id)).ToArray();
                    var volumeIds = scenarios.Select(item => item.OperationId("booking")).ToArray();
                    // Completed dated drives an agent handled belong to that agent's own history queue.
                    var completed = queue == "History"
                        ? DevelopmentTestDriveHistory.All.Where(item =>
                            item.Kind == DevelopmentTestDriveHistoryKind.Completed &&
                            item.Listing.Intake.BranchId == branch.Id &&
                            DevelopmentTestDriveHistory.HandlerFor(item) == agent.Id).Select(item => item.Id).ToArray()
                        : [];
                    var ids = volumeIds.Concat(completed).ToArray();
                    var expected = await database.TestDriveBookings.AsNoTracking().Where(item => ids.Contains(item.Id))
                        .OrderBy(item => item.StartsAt).ThenBy(item => item.Id).Select(item => item.Id).ToArrayAsync();
                    var oversightExpected = await database.TestDriveBookings.AsNoTracking()
                        .Where(item => volumeIds.Contains(item.Id)).OrderBy(item => item.StartsAt)
                        .ThenBy(item => item.Id).Select(item => item.Id).ToArrayAsync();
                    Assert.NotEmpty(expected);
                    var missing = ids.Except(expected).ToArray();
                    Assert.True(missing.Length == 0, $"{branch.Name} {queue} missing {missing.Length} of {ids.Length}: " +
                        string.Join(", ", DevelopmentTestDriveVolumeManifest.All.Where(item => missing.Contains(
                            item.OperationId("booking"))).Select(item => $"{item.Queue}#{item.Index}")));
                    var query = StaffTestDriveQuery.Parse(null, null, 1, 25, queue)!;
                    await AssertPagesAsync(store, agent.Id, query, expected);
                    // All registration values tie, so only the canonical UUID tie-breaker can prevent page drift.
                    var plates = await (from booking in database.TestDriveBookings.AsNoTracking()
                                        join vehicle in database.Vehicles.AsNoTracking() on booking.VehicleId equals vehicle.Id
                                        where ids.Contains(booking.Id)
                                        select new { booking.Id, vehicle.RegistrationPlate }).ToArrayAsync();
                    await AssertPagesAsync(store, agent.Id, query with { Sort = "registrationPlate", Direction = "desc" },
                        [.. plates.OrderByDescending(item => item.RegistrationPlate, StringComparer.Ordinal)
                            .ThenBy(item => item.Id).Select(item => item.Id)]);
                    if (queue == "ToClaim")
                    {
                        continue;
                    }

                    var oversight = query with
                    {
                        Queue = "",
                        BranchId = branch.Id,
                        Handler = agent.Email.ToUpperInvariant(),
                        Status = queue == "History" ? "Cancelled" : "Booked"
                    };
                    await AssertPagesAsync(store, manager.Id, oversight, oversightExpected);
                    await AssertPagesAsync(store, generalManager.Id, oversight, oversightExpected);
                }

                await AssertScheduledAsync(database, calendar, agent.Id, branch.Id, null, agent.Id);
                await AssertScheduledAsync(database, calendar, manager.Id, branch.Id, agent.Email, agent.Id);
                await AssertScheduledAsync(database, calendar, generalManager.Id, branch.Id, agent.Email, agent.Id);
            }

            await AssertScheduledAsync(database, calendar, manager.Id, branch.Id, "unassigned", null);
            var foreignBranch = DevelopmentFixtureManifest.Branches.First(item => item.Id != branch.Id).Id;
            Assert.Equal(TestDriveReadStatus.Forbidden, (await store.ListAsync(agents[0].Id,
                StaffTestDriveQuery.Parse(null, null, 1, 25, "MyWork", branchId: foreignBranch)!, CancellationToken.None)).Status);
        }
    }

    private static async Task AssertPagesAsync(IStaffTestDriveReadStore store, Guid actorId,
        StaffTestDriveQuery query, Guid[] expected)
    {
        var collected = new List<Guid>();
        for (var page = 1; page <= (expected.Length + 24) / 25 + 1; page++)
        {
            var result = await store.ListAsync(actorId, query with { Page = page }, CancellationToken.None);
            Assert.Equal(TestDriveReadStatus.Succeeded, result.Status);
            var snapshot = Assert.IsType<StaffTestDrivePage>(result.Value);
            Assert.Equal(expected.Length, snapshot.Total);
            Assert.Equal(expected.Skip((page - 1) * 25).Take(25), snapshot.Items.Select(item => item.Id));
            collected.AddRange(snapshot.Items.Select(item => item.Id));
        }

        Assert.Equal(expected, collected);
        var refreshed = await store.ListAsync(actorId, query, CancellationToken.None);
        Assert.Equal(expected.Take(25), refreshed.Value!.Items.Select(item => item.Id));
        var tail = await store.ListAsync(actorId, query with { Search = expected[^1].ToString() }, CancellationToken.None);
        Assert.Equal(1, tail.Value!.Total);
        Assert.Equal(expected[^1], Assert.Single(tail.Value.Items).Id);
    }

    private static async Task AssertScheduledAsync(DealershipDbContext database, IWorkCalendarStore calendar,
        Guid actorId, Guid branchId, string? employee, Guid? handlerId)
    {
        var now = await database.Database.SqlQuery<DateTimeOffset>($"SELECT clock_timestamp() AS \"Value\"").SingleAsync();
        var today = DateOnly.FromDateTime(AppointmentDay.StartUtc(now).AddHours(8).DateTime);
        var query = WorkCalendarQuery.Parse(today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            today.AddDays(30).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), branchId, employee)!;
        var result = await calendar.ScheduledAsync(actorId, query, CancellationToken.None);
        Assert.Equal(WorkCalendarReadStatus.Succeeded, result.Status);
        var snapshot = Assert.IsType<WorkCalendarSnapshot>(result.Value);
        var expected = DevelopmentTestDriveVolumeManifest.All.Where(item => item.BranchId == branchId &&
            (handlerId is null ? item.Queue == "ToClaim" : item.Queue != "ToClaim" && item.HandlerId == handlerId))
            .Select(item => item.OperationId("booking")).ToHashSet();
        Assert.NotEmpty(expected);
        var actual = snapshot.Items.Where(item => item.Kind == "TestDrive" && expected.Contains(item.Id)).ToArray();
        Assert.Equal(expected.Count, actual.Length);
        Assert.All(snapshot.Items, item =>
        {
            Assert.Equal(branchId, item.BranchId);
            Assert.Equal(handlerId, item.HandlerId);
        });
        Assert.Equal(snapshot.Items.Count, snapshot.Total);
        Assert.Equal(snapshot.Items.OrderBy(item => item.StartsAt).ThenBy(item => item.Kind, StringComparer.Ordinal)
            .ThenBy(item => item.Id), snapshot.Items);
        var reread = await calendar.ScheduledAsync(actorId, query, CancellationToken.None);
        Assert.Equal(snapshot.Items, reread.Value!.Items);
    }
}

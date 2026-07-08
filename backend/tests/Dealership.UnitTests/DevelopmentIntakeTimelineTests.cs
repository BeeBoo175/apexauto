using System.Globalization;
using Dealership.Application.Intake;
using Dealership.Domain.Common;
using Dealership.Domain.Intake;
using Dealership.Infrastructure.Development;

namespace Dealership.UnitTests;

[Trait("Category", "SeedPipeline")]
public sealed class DevelopmentIntakeTimelineTests
{
    private static readonly AppointmentSchedule Schedule = new(new TimeOnly(9, 0), new TimeOnly(18, 0),
        90, 30, 120, [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
            DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday]);

    [Theory]
    [InlineData("VCA 6241", "f15963a3-f386-8c74-9920-646ed466b0ce")]
    [InlineData("VCA 6242", "6f24fff2-e349-825b-a18c-6cc172f9eb97")]
    [InlineData("VCA 6243", "704096b6-d46d-8880-8395-2a642ff6d8b6")]
    [InlineData("VCA 6244", "b03faae3-11b3-84ca-92fd-c86dc7d78f7e")]
    [InlineData("VCA 6295", "4af75ada-48e6-83d3-bba7-b53a00014359")]
    [InlineData("VCA 6296", "780113e9-b69f-8b5c-b3f0-1a1b1b7eafb2")]
    [InlineData("VCA 6310", "53ec91b4-f4e1-81f3-9491-3ad72af6f5a1")]
    [InlineData("VCA 6323", "8076b3c4-e757-8f23-8c95-33c8167fd771")]
    public void CompactSnapshotRetainsHistoricalAppointmentIdentities(string plate, string appointmentId)
    {
        var appointment = Assert.Single(DevelopmentIntakeManifest.Appointments,
            item => item.RegistrationPlate == plate);
        Assert.Equal(Guid.Parse(appointmentId), appointment.Id);
    }

    [Fact]
    public void CheckInsAreSpreadAcrossEveryHandlerInEachBranch()
    {
        foreach (var branch in DevelopmentFixtureManifest.Branches)
        {
            var handlers = DevelopmentIntakeManifest.Appointments
                .Where(item => item.BranchId == branch.Id && item.CheckIn is not null)
                .GroupBy(item => DevelopmentFixtureManifest.CheckInHandlerFor(item.BranchId, item.Id).Id)
                .ToDictionary(group => group.Key, group => group.Count());
            var total = handlers.Values.Sum();
            Assert.Equal(3, handlers.Count);
            Assert.All(handlers.Values, count => Assert.InRange(count, total / 5, total * 3 / 5));
        }
    }

    [Fact]
    public void HistoryCoversSeveralMonthsWithFewArrivalsPerBranchDay()
    {
        var anchor = new DateTimeOffset(2026, 11, 6, 2, 0, 0, TimeSpan.Zero);
        var moments = DevelopmentIntakeTimeline.Create(Schedule, anchor);
        foreach (var branch in moments.GroupBy(item => item.Fixture.BranchId))
        {
            var perDay = branch.GroupBy(item => DateOnly.FromDateTime(
                TimeZoneInfo.ConvertTime(item.Slot.StartsAt, DealershipLocale.Malaysia).DateTime))
                .Select(group => group.Count()).ToArray();
            Assert.InRange(perDay.Max(), 1, DevelopmentIntakeTimeline.HistoricalArrivalsPerDay);
            var oldest = branch.Min(item => item.Slot.StartsAt);
            Assert.InRange((anchor - oldest).TotalDays, 60, 250);
        }
    }

    [Fact]
    public void SalesAndTestDrivesAreSpreadAcrossBothAgentsInEachBranch()
    {
        foreach (var branch in DevelopmentFixtureManifest.Branches)
        {
            var testDrives = DevelopmentTestDriveVolumeManifest.All
                .Where(item => item.BranchId == branch.Id && item.Queue != "ToClaim")
                .GroupBy(item => item.HandlerId).ToDictionary(group => group.Key, group => group.Count());
            var sales = DevelopmentSaleVolumeManifest.All
                .Where(item => item.Hold.Listing.Intake.BranchId == branch.Id)
                .GroupBy(item => item.AgentId).ToDictionary(group => group.Key, group => group.Count());
            foreach (var spread in new[] { testDrives, sales })
            {
                var total = spread.Values.Sum();
                Assert.Equal(2, spread.Count);
                Assert.All(spread.Values, count => Assert.InRange(count, total / 4, total * 3 / 4));
            }
        }
    }

    [Fact]
    public void HistoricalWorkshopInspectionsCoverEveryBranch()
    {
        foreach (var branch in DevelopmentFixtureManifest.Branches)
        {
            Assert.Contains(DevelopmentIntakeManifest.Appointments,
                item => item.BranchId == branch.Id && DevelopmentWorkshopHistory.Covers(item));
        }
    }

    [Theory]
    [InlineData("VCA 6323")]
    [InlineData("BPL 6323")]
    [InlineData("PQA 6323")]
    public void AppendedSalesCohortPreservesOriginalActiveReservationListing(string plate)
    {
        var original = Assert.Single(DevelopmentIntakeManifest.CatalogueExamples,
            item => item.RegistrationPlate == plate);
        var hold = Assert.Single(DevelopmentReservationScenarios.All,
            item => item.Index == 51 && item.Listing.Intake.BranchId == original.BranchId);
        Assert.Equal(original.Id, hold.Listing.Id);
    }

    [Theory]
    [InlineData("2026-10-18T00:00:00+08:00")]
    [InlineData("2026-10-19T18:01:00+08:00")]
    [InlineData("2026-10-19T23:59:59.9999999+08:00")]
    [InlineData("2026-10-20T00:00:00+08:00")]
    [InlineData("2026-12-31T23:59:59.9999999+08:00")]
    [InlineData("2027-01-01T00:00:00+08:00")]
    public void SnapshotDatesObeyBusinessScheduleAndHistoricalTransitions(string date)
    {
        var anchor = DateTimeOffset.Parse(date, CultureInfo.InvariantCulture);
        var timeline = DevelopmentIntakeTimeline.Create(Schedule, anchor);
        Assert.Equal(DevelopmentIntakeManifest.Appointments.Count, timeline.Count);
        foreach (var branch in timeline.GroupBy(item => item.Fixture.BranchId))
        {
            Assert.Equal(DevelopmentIntakeManifest.TotalPerBranch, branch.Select(item => item.Slot.StartsAt).Distinct().Count());
            Assert.Equal(12, branch.Count(item => item.Fixture.Status == StaffWorklistStatus.Booked));
            var examples = DevelopmentIntakeManifest.InspectionExamples.Select(item => item.Id).ToHashSet();
            var catalogue = DevelopmentIntakeManifest.CatalogueExamples.Select(item => item.Id).ToHashSet();
            var workshop = DevelopmentIntakeManifest.WorkshopExamples.Select(item => item.Id).ToHashSet();
            var sales = DevelopmentIntakeManifest.SalesExamples.Select(item => item.Id).ToHashSet();
            Assert.Equal(14, branch.Count(item => examples.Contains(item.Fixture.Id)));
            Assert.Equal(14, branch.Count(item => catalogue.Contains(item.Fixture.Id)));
            Assert.Equal(DevelopmentIntakeManifest.WorkshopExamplesPerBranch,
                branch.Count(item => workshop.Contains(item.Fixture.Id)));
            Assert.Equal(DevelopmentIntakeManifest.SalesExamplesPerBranch,
                branch.Count(item => sales.Contains(item.Fixture.Id)));
            Assert.Empty(workshop.Intersect(sales));
            var oldestWorkshop = branch.Where(item => workshop.Contains(item.Fixture.Id)).Min(item => item.Slot.StartsAt);
            Assert.All(branch.Where(item => sales.Contains(item.Fixture.Id)), item => Assert.True(item.Slot.StartsAt < oldestWorkshop));
            var newestBaseline = branch.Where(item => item.Fixture.Status == StaffWorklistStatus.CheckedIn &&
                !examples.Contains(item.Fixture.Id) && !catalogue.Contains(item.Fixture.Id)).Max(item => item.Slot.StartsAt);
            Assert.All(branch.Where(item => examples.Contains(item.Fixture.Id)), item => Assert.True(item.Slot.StartsAt > newestBaseline));
            var noShow = branch.Single(item => item.Fixture.Status == StaffWorklistStatus.NoShow);
            Assert.All(branch.Where(item => catalogue.Contains(item.Fixture.Id)), item => Assert.True(item.Slot.StartsAt < noShow.Slot.StartsAt));
        }

        foreach (var moment in timeline)
        {
            Assert.Equal(moment.Slot, Schedule.FindSlot(moment.Slot.StartsAt, moment.BookedAt));
            Assert.NotEqual(DayOfWeek.Sunday, TimeZoneInfo.ConvertTime(moment.Slot.StartsAt, DealershipLocale.Malaysia).DayOfWeek);
            if (moment.Fixture.Status == StaffWorklistStatus.Booked)
            {
                Assert.True(moment.Slot.StartsAt > anchor);
                Assert.Equal(anchor, moment.BookedAt);
                Assert.Null(moment.TransitionAt);
                continue;
            }

            Assert.True(moment.Slot.EndsAt < anchor);
            Assert.True(moment.TransitionAt >= moment.BookedAt);
            Assert.True(moment.TransitionAt <= anchor);
            if (moment.Fixture.Status == StaffWorklistStatus.Cancelled)
            {
                Assert.True(moment.TransitionAt < moment.Slot.StartsAt);
                continue;
            }

            if (moment.Fixture.Status == StaffWorklistStatus.NoShow)
            {
                Assert.Equal(AppointmentDay.EndsAt(moment.Slot.StartsAt), moment.TransitionAt);
                continue;
            }

            Assert.Equal(TimeZoneInfo.ConvertTime(moment.Slot.StartsAt, DealershipLocale.Malaysia).Date,
                TimeZoneInfo.ConvertTime(moment.TransitionAt!.Value, DealershipLocale.Malaysia).Date);
        }
    }

    [Fact]
    public void InsufficientConfiguredCapacityFailsBeforeInitialization()
    {
        var schedule = new AppointmentSchedule(new TimeOnly(9, 0), new TimeOnly(18, 0),
            90, 1, 10, [DayOfWeek.Monday]);
        Assert.Throws<InvalidOperationException>(() => DevelopmentIntakeTimeline.Create(schedule,
            DateTimeOffset.Parse("2026-10-18T12:00:00+08:00", CultureInfo.InvariantCulture)));
    }
}

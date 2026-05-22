using System.Globalization;
using Dealership.Application.Intake;
using Dealership.Domain.Common;
using Dealership.Domain.Intake;
using Dealership.Infrastructure.Development;

namespace Dealership.UnitTests;

public sealed class DevelopmentIntakeTimelineTests
{
    private static readonly TimeZoneInfo Malaysia = TimeZoneInfo.FindSystemTimeZoneById(DealershipLocale.MalaysiaTimeZoneId);
    private static readonly InspectionSchedule Schedule = new(new TimeOnly(9, 0), new TimeOnly(18, 0),
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
            Assert.Equal(14, branch.Count(item => examples.Contains(item.Fixture.Id)));
            Assert.Equal(14, branch.Count(item => catalogue.Contains(item.Fixture.Id)));
            var newestBaseline = branch.Where(item => item.Fixture.Status == StaffWorklistStatus.CheckedIn &&
                !examples.Contains(item.Fixture.Id) && !catalogue.Contains(item.Fixture.Id)).Max(item => item.Slot.StartsAt);
            Assert.All(branch.Where(item => examples.Contains(item.Fixture.Id)), item => Assert.True(item.Slot.StartsAt > newestBaseline));
            var noShow = branch.Single(item => item.Fixture.Status == StaffWorklistStatus.NoShow);
            Assert.All(branch.Where(item => catalogue.Contains(item.Fixture.Id)), item => Assert.True(item.Slot.StartsAt < noShow.Slot.StartsAt));
        }

        foreach (var moment in timeline)
        {
            Assert.Equal(moment.Slot, Schedule.FindSlot(moment.Slot.StartsAt, moment.BookedAt));
            Assert.NotEqual(DayOfWeek.Sunday, TimeZoneInfo.ConvertTime(moment.Slot.StartsAt, Malaysia).DayOfWeek);
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

            Assert.Equal(TimeZoneInfo.ConvertTime(moment.Slot.StartsAt, Malaysia).Date,
                TimeZoneInfo.ConvertTime(moment.TransitionAt!.Value, Malaysia).Date);
        }
    }

    [Fact]
    public void InsufficientConfiguredCapacityFailsBeforeInitialization()
    {
        var schedule = new InspectionSchedule(new TimeOnly(9, 0), new TimeOnly(18, 0),
            90, 1, 10, [DayOfWeek.Monday]);
        Assert.Throws<InvalidOperationException>(() => DevelopmentIntakeTimeline.Create(schedule,
            DateTimeOffset.Parse("2026-10-18T12:00:00+08:00", CultureInfo.InvariantCulture)));
    }
}

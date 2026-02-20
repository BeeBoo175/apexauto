using Dealership.Application.Intake;
using Dealership.Domain.Common;
using Dealership.Domain.Intake;

namespace Dealership.Infrastructure.Development;

public sealed record DevelopmentIntakeMoment(DevelopmentIntakeFixture Fixture, InspectionSlot Slot,
    DateTimeOffset BookedAt, DateTimeOffset? TransitionAt);

public static class DevelopmentIntakeTimeline
{
    private static readonly TimeZoneInfo Malaysia = TimeZoneInfo.FindSystemTimeZoneById(DealershipLocale.MalaysiaTimeZoneId);

    public static IReadOnlyList<DevelopmentIntakeMoment> Create(InspectionSchedule schedule, DateTimeOffset anchor)
    {
        var future = Slots(schedule, anchor).Where(slot => slot.StartsAt > anchor)
            .Take(DevelopmentIntakeManifest.AppointmentsPerBranch).ToArray();
        if (future.Length != DevelopmentIntakeManifest.AppointmentsPerBranch)
        {
            throw new InvalidOperationException("The configured schedule cannot fit the development worklist within its booking horizon.");
        }

        var historical = HistoricalSlots(schedule, anchor);
        return [.. DevelopmentIntakeManifest.Appointments.GroupBy(item => item.BranchId).SelectMany(branch =>
        {
            var next = 0;
            var checkedIn = 1;
            return branch.Select(fixture => fixture.Status switch
            {
                StaffWorklistStatus.Cancelled => new DevelopmentIntakeMoment(fixture, historical[0].Slot,
                    historical[0].BookedAt, historical[0].Slot.StartsAt.AddMinutes(-1)),
                StaffWorklistStatus.NoShow => new DevelopmentIntakeMoment(fixture, historical[^1].Slot,
                    historical[^1].BookedAt, AppointmentDay.EndsAt(historical[^1].Slot.StartsAt)),
                StaffWorklistStatus.CheckedIn => CheckedIn(fixture, historical[checkedIn++]),
                _ => new DevelopmentIntakeMoment(fixture, future[next++], anchor, null)
            });
        }).OrderBy(moment => moment.Slot.StartsAt).ThenBy(moment => moment.Fixture.Id)];
    }

    private static (InspectionSlot Slot, DateTimeOffset BookedAt)[] HistoricalSlots(InspectionSchedule schedule,
        DateTimeOffset anchor)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(anchor, Malaysia).DateTime);
        var result = new List<(InspectionSlot, DateTimeOffset)>();
        var required = DevelopmentIntakeManifest.CheckedInPerBranch + 2;
        for (var days = 1; days <= 366 && result.Count < required; days++)
        {
            var date = today.AddDays(-days);
            var localMidnight = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
            var bookedAt = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localMidnight, Malaysia));
            var slots = schedule.GetSlots(date, bookedAt);
            if (slots.Count > 0)
            {
                result.Add((slots[0], bookedAt));
            }
        }

        if (result.Count != required)
        {
            throw new InvalidOperationException("The configured schedule cannot provide historical development appointments.");
        }

        return [.. result];
    }

    private static DevelopmentIntakeMoment CheckedIn(DevelopmentIntakeFixture fixture,
        (InspectionSlot Slot, DateTimeOffset BookedAt) moment) =>
        new(fixture, moment.Slot, moment.BookedAt, moment.Slot.StartsAt);

    private static IEnumerable<InspectionSlot> Slots(InspectionSchedule schedule, DateTimeOffset now)
    {
        var window = schedule.GetDateWindow(now);
        for (var date = window.FirstDate; date <= window.LastDate; date = date.AddDays(1))
        {
            foreach (var slot in schedule.GetSlots(date, now))
            {
                yield return slot;
            }
        }
    }
}

using Dealership.Application.Intake;
using Dealership.Domain.Common;
using Dealership.Domain.Intake;

namespace Dealership.Infrastructure.Development;

public sealed record DevelopmentIntakeMoment(DevelopmentIntakeFixture Fixture, AppointmentSlot Slot,
    DateTimeOffset BookedAt, DateTimeOffset? TransitionAt);

public static class DevelopmentIntakeTimeline
{

    public static IReadOnlyList<DevelopmentIntakeMoment> Create(AppointmentSchedule schedule, DateTimeOffset anchor)
    {
        var upcoming = Slots(schedule, anchor).Where(slot => slot.StartsAt > anchor).ToArray();
        if (upcoming.Length < DevelopmentIntakeManifest.AppointmentsPerBranch)
        {
            throw new InvalidOperationException("The configured schedule cannot fit the development worklist within its booking horizon.");
        }

        // Evenly spaced slots keep the calendar populated from tomorrow to the end of the booking horizon.
        var stride = upcoming.Length / DevelopmentIntakeManifest.AppointmentsPerBranch;
        var future = Enumerable.Range(0, DevelopmentIntakeManifest.AppointmentsPerBranch)
            .Select(index => upcoming[index * stride]).ToArray();

        var historical = HistoricalSlots(schedule, anchor);
        var examples = DevelopmentIntakeManifest.InspectionExamples.Select(item => item.Id).ToHashSet();
        var catalogue = DevelopmentIntakeManifest.CatalogueExamples.Select(item => item.Id).ToHashSet();
        var workshop = DevelopmentIntakeManifest.WorkshopExamples.Select(item => item.Id).ToHashSet();
        var sales = DevelopmentIntakeManifest.SalesExamples.Select(item => item.Id).ToHashSet();
        return [.. DevelopmentIntakeManifest.Appointments.GroupBy(item => item.BranchId).SelectMany(branch =>
        {
            var next = 0;
            var checkedIn = DevelopmentIntakeManifest.InspectionExamplesPerBranch + 1;
            var example = 1;
            var catalogueIndex = DevelopmentIntakeManifest.InspectionCheckedInPerBranch + 2;
            var workshopIndex = catalogueIndex + DevelopmentIntakeManifest.CatalogueExamplesPerBranch;
            var salesIndex = workshopIndex + DevelopmentIntakeManifest.WorkshopExamplesPerBranch;
            var (noShowSlot, noShowBookedAt) = historical[DevelopmentIntakeManifest.InspectionCheckedInPerBranch + 1];
            return branch.Select(fixture => fixture.Status switch
            {
                StaffWorklistStatus.Cancelled => new DevelopmentIntakeMoment(fixture, historical[0].Slot,
                    historical[0].BookedAt, historical[0].Slot.StartsAt.AddMinutes(-1)),
                StaffWorklistStatus.NoShow => new DevelopmentIntakeMoment(fixture, noShowSlot,
                    noShowBookedAt, AppointmentDay.EndsAt(noShowSlot.StartsAt)),
                StaffWorklistStatus.CheckedIn when catalogue.Contains(fixture.Id) => CheckedIn(fixture, historical[catalogueIndex++]),
                StaffWorklistStatus.CheckedIn when workshop.Contains(fixture.Id) => CheckedIn(fixture, historical[workshopIndex++]),
                StaffWorklistStatus.CheckedIn when sales.Contains(fixture.Id) => CheckedIn(fixture, historical[salesIndex++]),
                StaffWorklistStatus.CheckedIn => CheckedIn(fixture, historical[examples.Contains(fixture.Id) ? example++ : checkedIn++]),
                _ => new DevelopmentIntakeMoment(fixture, future[next++], anchor, null)
            });
        }).OrderBy(moment => moment.Slot.StartsAt).ThenBy(moment => moment.Fixture.Id)];
    }

    private static (AppointmentSlot Slot, DateTimeOffset BookedAt)[] HistoricalSlots(AppointmentSchedule schedule,
        DateTimeOffset anchor)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(anchor, DealershipLocale.Malaysia).DateTime);
        var result = new List<(AppointmentSlot, DateTimeOffset)>();
        var required = DevelopmentIntakeManifest.CheckedInPerBranch + 2;
        for (var days = 1; days <= 366 && result.Count < required; days++)
        {
            var date = today.AddDays(-days);
            var localMidnight = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
            var bookedAt = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localMidnight, DealershipLocale.Malaysia));
            var slots = schedule.GetSlots(date, bookedAt);
            if (slots.Count > 0)
            {
                // Rotating the slot by day gives past appointments a spread of weekdays and times.
                result.Add((slots[days % slots.Count], bookedAt));
            }
        }

        if (result.Count != required)
        {
            throw new InvalidOperationException("The configured schedule cannot provide historical development appointments.");
        }

        return [.. result];
    }

    private static DevelopmentIntakeMoment CheckedIn(DevelopmentIntakeFixture fixture,
        (AppointmentSlot Slot, DateTimeOffset BookedAt) moment) =>
        new(fixture, moment.Slot, moment.BookedAt, moment.Slot.StartsAt);

    private static IEnumerable<AppointmentSlot> Slots(AppointmentSchedule schedule, DateTimeOffset now)
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

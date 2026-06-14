using Dealership.Domain.Common;
using Dealership.Domain.Intake;
using Dealership.Domain.TestDrives;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Development;

// Mechanical slot discovery shared by the test-drive fixtures; each fixture still applies its own conflict rule.
internal static class DevelopmentTestDriveSlots
{
    private const int HorizonDays = 30;

    public static async Task<AppointmentSlot[]> UpcomingAsync(DealershipDbContext database,
        AppointmentSchedule schedule, CancellationToken cancellationToken)
    {
        var now = await database.DatabaseNowAsync(cancellationToken);
        var date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, DealershipLocale.Malaysia).DateTime);
        return Enumerable.Range(0, HorizonDays).SelectMany(day =>
            schedule.GetSlots(date.AddDays(day), now, TestDriveBooking.Duration)).ToArray();
    }

    public static Task<DateTimeOffset[]> ActiveStartsAsync(DealershipDbContext database, Guid branchId,
        CancellationToken cancellationToken)
    {
        return database.TestDriveBookings.AsNoTracking().Where(item => item.BranchId == branchId &&
            !database.TestDriveCancellations.Any(cancellation => cancellation.BookingId == item.Id))
            .Select(item => item.StartsAt).ToArrayAsync(cancellationToken);
    }
}

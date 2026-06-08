using System.Globalization;
using Dealership.Domain.Common;
using Dealership.Domain.TestDrives;

namespace Dealership.Application.TestDrives;

public sealed class TestDriveDiscoveryService(ITestDriveReadStore reads, AppointmentSchedule schedule)
{

    public async Task<TestDriveSlotResult> GetSlotsAsync(Guid vehicleId, string? date,
        CancellationToken cancellationToken)
    {
        if (vehicleId == Guid.Empty || !DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var requestedDate) || requestedDate == DateOnly.MinValue || requestedDate == DateOnly.MaxValue)
        {
            return new(TestDriveReadStatus.Invalid);
        }

        var from = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(
            requestedDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), DealershipLocale.Malaysia));
        var snapshot = await reads.SlotsAsync(vehicleId, from, from.AddDays(1), cancellationToken);
        if (snapshot is null)
        {
            return new(TestDriveReadStatus.NotFound);
        }

        var window = schedule.GetDateWindow(snapshot.Now);
        if (requestedDate < window.FirstDate || requestedDate > window.LastDate)
        {
            return new(TestDriveReadStatus.Invalid);
        }

        var slots = schedule.GetSlots(requestedDate, snapshot.Now, TestDriveBooking.Duration)
            .Where(slot => !snapshot.Occupied.Any(booked => booked.StartsAt < slot.EndsAt && slot.StartsAt < booked.EndsAt))
            .ToArray();
        return new(TestDriveReadStatus.Succeeded,
            new(vehicleId, snapshot.BranchId, snapshot.PublicationId, requestedDate, window, slots));
    }
}

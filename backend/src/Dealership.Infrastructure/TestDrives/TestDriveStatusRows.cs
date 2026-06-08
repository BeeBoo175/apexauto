using Dealership.Domain.TestDrives;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.TestDrives;

internal static class TestDriveStatusRows
{
    // One SQL-translatable lifecycle projection serves customer history and staff filtering/sorting.
    public static IQueryable<TestDriveStatusRow> Create(DealershipDbContext database, DateTimeOffset today)
    {
        return from booking in database.TestDriveBookings.AsNoTracking()
               let cancelled = database.TestDriveCancellations.Any(item => item.BookingId == booking.Id)
               let started = database.TestDriveStarts.Any(item => item.BookingId == booking.Id)
               let outcome = database.TestDriveOutcomes.Where(item => item.BookingId == booking.Id)
                   .Select(item => item.Kind).FirstOrDefault()
               select new TestDriveStatusRow
               {
                   Id = booking.Id,
                   Status = (cancelled ? TestDriveStatus.Cancelled : null) ?? outcome ??
                       (started ? TestDriveStatus.InProgress : null) ?? (booking.StartsAt < today ? TestDriveStatus.NoShow : TestDriveStatus.Booked)
               };
    }
}

internal sealed class TestDriveStatusRow
{
    public Guid Id { get; init; }

    public string Status { get; init; } = string.Empty;
}

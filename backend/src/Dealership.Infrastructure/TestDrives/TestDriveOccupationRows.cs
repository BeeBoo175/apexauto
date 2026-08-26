using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.TestDrives;

internal static class TestDriveOccupationRows
{
    // Only a deliberate outcome releases active occupation, regardless of the booked end time.
    public static IQueryable<TestDriveOccupation> Active(DealershipDbContext database)
    {
        return database.TestDriveOccupations.AsNoTracking().Where(item => item.OutcomeId == null);
    }

    // Customers with a test drive still to come or in progress; a customer may hold one at a time. The booking
    // admission and the staff customer lookup both apply this one rule.
    public static IQueryable<Guid> CustomersWithOutstanding(DealershipDbContext database, DateTimeOffset today)
    {
        return database.TestDriveBookings
            .Where(item => (item.StartsAt >= today || Active(database).Any(occupation => occupation.BookingId == item.Id)) &&
                !database.TestDriveOutcomes.Any(outcome => outcome.BookingId == item.Id) &&
                !database.TestDriveCancellations.Any(cancellation => cancellation.BookingId == item.Id))
            .Select(item => item.CustomerId);
    }
}

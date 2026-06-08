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
}

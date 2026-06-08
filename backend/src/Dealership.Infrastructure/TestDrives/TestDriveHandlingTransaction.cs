using Dealership.Application.TestDrives;
using Dealership.Domain.TestDrives;
using Dealership.Infrastructure.Persistence;
using Dealership.Infrastructure.Vehicles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Dealership.Infrastructure.TestDrives;

internal sealed class TestDriveHandlingTransaction(DealershipDbContext database, IDbContextTransaction transaction,
    TestDriveHandlingContext context) : ITestDriveHandlingTransaction
{
    public TestDriveHandlingContext Context => context;

    public Task<DateTimeOffset> ReadDatabaseTimeAsync(CancellationToken cancellationToken)
    {
        return database.DatabaseNowAsync(cancellationToken);
    }

    public Task<bool> IsVehicleEligibleAsync(CancellationToken cancellationToken)
    {
        return PublicListingQuery.Read(database).AnyAsync(item => item.Vehicle.Id == context.Booking!.VehicleId,
            cancellationToken);
    }

    public async Task<bool> HasStartConflictAsync(Guid handlerId, DateTimeOffset startsAt,
        CancellationToken cancellationToken)
    {
        var booking = context.Booking!;
        if (await TestDriveOccupationRows.Active(database).AnyAsync(item => item.VehicleId == booking.VehicleId, cancellationToken))
        {
            return true;
        }

        var endsAt = startsAt + TestDriveBooking.Duration;
        if (await database.TestDriveBookings.AnyAsync(item => item.Id != booking.Id &&
            item.VehicleId == booking.VehicleId && item.StartsAt < endsAt && startsAt < item.EndsAt &&
            !database.TestDriveCancellations.Any(cancelled => cancelled.BookingId == item.Id) &&
            !database.TestDriveOutcomes.Any(outcome => outcome.BookingId == item.Id), cancellationToken))
        {
            return true;
        }

        return await StaffScheduleConflicts.HasOverlapAsync(database, handlerId, startsAt, endsAt,
            null, booking.Id, cancellationToken);
    }

    public async Task CommitStartAsync(TestDriveStart start, CancellationToken cancellationToken)
    {
        database.TestDriveStarts.Add(start);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task CommitOutcomeAsync(TestDriveOutcome outcome, CancellationToken cancellationToken)
    {
        database.TestDriveOutcomes.Add(outcome);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await transaction.DisposeAsync();
        }
        finally
        {
            database.ChangeTracker.Clear();
        }
    }
}

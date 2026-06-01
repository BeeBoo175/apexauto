using Dealership.Application.TestDrives;
using Dealership.Domain.TestDrives;
using Dealership.Infrastructure.Intake;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Storage;

namespace Dealership.Infrastructure.TestDrives;

internal sealed class TestDriveAssignmentTransaction(DealershipDbContext database, IDbContextTransaction transaction,
    TestDriveAssignmentContext context) : ITestDriveAssignmentTransaction
{
    public TestDriveAssignmentContext Context => context;

    public Task<DateTimeOffset> ReadDatabaseTimeAsync(CancellationToken cancellationToken)
    {
        return database.DatabaseNowAsync(cancellationToken);
    }

    public async Task<bool> HasOverlapAsync(Guid handlerId, CancellationToken cancellationToken)
    {
        var booking = context.Booking!;
        var startsAt = context.Started ? await database.DatabaseNowAsync(cancellationToken) : booking.StartsAt;
        var endsAt = context.Started ? startsAt + TestDriveBooking.Duration : booking.EndsAt;
        return await StaffScheduleConflicts.HasOverlapAsync(database, handlerId, startsAt, endsAt,
            null, booking.Id, cancellationToken);
    }

    public async Task CommitAsync(TestDriveAssignment assignment, CancellationToken cancellationToken)
    {
        database.TestDriveAssignments.Add(assignment);
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

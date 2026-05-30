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

    public Task<bool> HasOverlapAsync(Guid handlerId, CancellationToken cancellationToken)
    {
        var booking = context.Booking!;
        return StaffScheduleConflicts.HasOverlapAsync(database, handlerId, booking.StartsAt, booking.EndsAt,
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

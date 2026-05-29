using Dealership.Application.TestDrives;
using Dealership.Domain.TestDrives;
using Dealership.Infrastructure.Mail;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Dealership.Infrastructure.TestDrives;

internal sealed class TestDriveCancellationTransaction(DealershipDbContext database, IDbContextTransaction transaction,
    MailDeliverySignal mail, TestDriveCancellationContext context, string recipient) : ITestDriveCancellationTransaction
{
    public TestDriveCancellationContext Context => context;

    public async Task<TestDriveCancellationResult> CommitAsync(TestDriveCancellation cancellation, CancellationToken cancellationToken)
    {
        database.TestDriveCancellations.Add(cancellation);
        database.TestDriveMailOutbox.Add(new TestDriveMail(cancellation, recipient));
        try
        {
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            mail.Request();
            return new(TestDriveCancellationStatus.Succeeded, cancellation);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        { ConstraintName: "CK_TestDriveCancellations_Time" or "AK_TestDriveCancellations_BookingId" })
        {
            await transaction.RollbackAsync(cancellationToken);
            return new(TestDriveCancellationStatus.Conflict);
        }
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

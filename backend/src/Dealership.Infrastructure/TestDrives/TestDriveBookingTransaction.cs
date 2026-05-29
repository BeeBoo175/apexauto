using Dealership.Application.TestDrives;
using Dealership.Domain.TestDrives;
using Dealership.Infrastructure.Mail;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Dealership.Infrastructure.TestDrives;

internal sealed class TestDriveBookingTransaction(DealershipDbContext database, IDbContextTransaction transaction,
    MailDeliverySignal mail, TestDriveBookingContext context, string recipient) : ITestDriveBookingTransaction
{
    public TestDriveBookingContext Context => context;

    public async Task<TestDriveBookingResult> CommitAsync(TestDriveBooking booking, CancellationToken cancellationToken)
    {
        database.TestDriveBookings.Add(booking);
        database.TestDriveMailOutbox.Add(new TestDriveMail(booking, recipient));
        try
        {
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            mail.Request();
            return new(TestDriveBookingStatus.Succeeded, booking);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        { ConstraintName: "EX_TestDriveBookings_Slot" or "CK_TestDriveBookings_CustomerLimit" or "CK_TestDriveBookings_Eligibility" })
        {
            await transaction.RollbackAsync(cancellationToken);
            return new(TestDriveBookingStatus.Conflict);
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

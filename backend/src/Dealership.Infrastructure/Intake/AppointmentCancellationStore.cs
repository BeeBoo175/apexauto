using Dealership.Application.Intake;
using Dealership.Infrastructure.Mail;
using Dealership.Infrastructure.Persistence;
using Dealership.Infrastructure.Photos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Dealership.Infrastructure.Intake;

public sealed class AppointmentCancellationStore(DealershipDbContext database, IOptions<IntakePhotoSettings> settings,
    MailDeliverySignal mail, PhotoCleanupSignal cleanup) : IAppointmentCancellationStore
{
    public async Task<IAppointmentCancellationTransaction> LockAsync(Guid ownerId, Guid appointmentId,
        CancellationToken cancellationToken)
    {
        var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            await database.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({"appointment:" + appointmentId}, 0))", cancellationToken);
            var user = await database.Users.FromSqlInterpolated(
                $"SELECT * FROM \"AspNetUsers\" WHERE \"Id\" = {ownerId} FOR UPDATE")
                .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
            var hasProfile = await database.CustomerProfiles.AnyAsync(profile => profile.UserId == ownerId, cancellationToken);
            var appointment = await database.IntakeAppointments.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == appointmentId && item.OwnerId == ownerId, cancellationToken);

            if (appointment is not null)
            {
                await database.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT 1 FROM \"Branches\" WHERE \"Id\" = {appointment.BranchId} FOR UPDATE", cancellationToken);
                appointment = await database.IntakeAppointments.AsNoTracking()
                    .SingleAsync(item => item.Id == appointmentId && item.OwnerId == ownerId, cancellationToken);
            }

            var checkedIn = await database.AppointmentCheckIns.AnyAsync(item => item.AppointmentId == appointmentId, cancellationToken);
            var context = new CancellationContext(
                user is null ? null : new CancellationCustomer(user.IsActive, user.EmailConfirmed, user.Role, hasProfile),
                appointment, checkedIn, TimeSpan.FromDays(settings.Value.CancelledRetentionDays));
            return new AppointmentCancellationTransaction(database, transaction, context, user?.Email, mail, cleanup);
        }
        catch
        {
            try
            {
                await transaction.DisposeAsync();
            }
            finally
            {
                database.ChangeTracker.Clear();
            }

            throw;
        }
    }
}

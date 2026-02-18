using Dealership.Application.Intake;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Microsoft.Extensions.Options;
using Dealership.Infrastructure.Mail;

namespace Dealership.Infrastructure.Intake;

public sealed class AppointmentCancellationStore(DealershipDbContext database, TimeProvider clock,
    IOptions<IntakePhotoSettings> settings, MailDeliverySignal mail) : IAppointmentCancellationStore
{
    public async Task<CancellationResult> CancelAsync(Guid ownerId, Guid appointmentId, CancellationToken cancellationToken)
    {
        if (appointmentId == Guid.Empty)
        {
            return new CancellationResult(CancellationStatus.Invalid);
        }

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        await database.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({"appointment:" + appointmentId}, 0))", cancellationToken);
        var user = await VerifiedCustomerQuery.ForOwner(database, ownerId, lockUser: true)
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            return new CancellationResult(CancellationStatus.Forbidden);
        }

        var appointment = await database.IntakeAppointments.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == appointmentId && item.OwnerId == ownerId, cancellationToken);

        if (appointment is null)
        {
            return new CancellationResult(CancellationStatus.NotFound);
        }

        await database.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM \"Branches\" WHERE \"Id\" = {appointment.BranchId} FOR UPDATE", cancellationToken);
        var existing = await database.IntakeAppointments.Where(item => item.Id == appointmentId)
            .Select(item => item.CancelledAt).SingleAsync(cancellationToken);

        if (existing is not null)
        {
            return new CancellationResult(CancellationStatus.Succeeded, new CancellationReceipt(appointmentId, existing.Value));
        }

        if (await database.AppointmentCheckIns.AnyAsync(item => item.AppointmentId == appointmentId, cancellationToken))
        {
            return new CancellationResult(CancellationStatus.CheckedIn);
        }

        var now = clock.GetUtcNow();
        now = new DateTimeOffset(now.UtcTicks - now.UtcTicks % 10, TimeSpan.Zero);

        if (now < appointment.CreatedAt || now >= appointment.StartsAt)
        {
            return new CancellationResult(CancellationStatus.TooLate);
        }

        appointment.Cancel(now, TimeSpan.FromDays(settings.Value.CancelledRetentionDays));
        database.AppointmentMailOutbox.Add(new AppointmentMail(appointmentId, AppointmentMailKind.Cancellation,
            user.Email!, now));
        try
        {
            await database.IntakeAppointments.Where(item => item.Id == appointmentId)
                .ExecuteUpdateAsync(update => update
                    .SetProperty(item => item.CancelledAt, appointment.CancelledAt)
                    .SetProperty(item => item.CancellationPhotosExpireAt, appointment.CancellationPhotosExpireAt), cancellationToken);
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            mail.Request();
        }
        catch (PostgresException exception) when (exception.ConstraintName is
            "CK_IntakeAppointments_CancellationTime" or "CK_IntakeAppointments_CheckedIn")
        {
            await transaction.RollbackAsync(cancellationToken);
            database.ChangeTracker.Clear();
            return new CancellationResult(exception.ConstraintName == "CK_IntakeAppointments_CheckedIn"
                ? CancellationStatus.CheckedIn
                : CancellationStatus.TooLate);
        }
        return new CancellationResult(CancellationStatus.Succeeded, new CancellationReceipt(appointmentId, now));
    }
}

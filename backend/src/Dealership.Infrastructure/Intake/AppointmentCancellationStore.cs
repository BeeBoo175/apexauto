using Dealership.Application.Intake;
using Dealership.Domain.Intake;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Microsoft.Extensions.Options;

namespace Dealership.Infrastructure.Intake;

public sealed class AppointmentCancellationStore(DealershipDbContext database, TimeProvider clock,
    IOptions<IntakePhotoSettings> settings) : IAppointmentCancellationStore
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
        var existing = await database.AppointmentCancellations.AsNoTracking()
            .SingleOrDefaultAsync(item => item.AppointmentId == appointmentId, cancellationToken);

        if (existing is not null)
        {
            return new CancellationResult(CancellationStatus.Succeeded, new CancellationReceipt(appointmentId, existing.CancelledAt));
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

        var cancellation = new AppointmentCancellation(appointment, now, TimeSpan.FromDays(settings.Value.CancelledRetentionDays));
        database.AppointmentCancellations.Add(cancellation);
        database.AppointmentMailOutbox.Add(new AppointmentMail(appointmentId, AppointmentMailKind.Cancellation,
            user.Email!, now));
        try
        {
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        { ConstraintName: "CK_AppointmentCancellations_Time" or "CK_AppointmentCancellations_CheckedIn" })
        {
            await transaction.RollbackAsync(cancellationToken);
            database.ChangeTracker.Clear();
            var error = (PostgresException)exception.InnerException!;
            return new CancellationResult(error.ConstraintName == "CK_AppointmentCancellations_CheckedIn"
                ? CancellationStatus.CheckedIn
                : CancellationStatus.TooLate);
        }
        return new CancellationResult(CancellationStatus.Succeeded, new CancellationReceipt(appointmentId, now));
    }
}

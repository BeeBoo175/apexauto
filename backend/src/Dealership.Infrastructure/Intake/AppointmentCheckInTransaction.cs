using Dealership.Application.Intake;
using Dealership.Domain.Intake;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Mail;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Dealership.Infrastructure.Intake;

internal sealed class AppointmentCheckInTransaction(DealershipDbContext database, IDbContextTransaction transaction,
    CheckInContext context, Guid appointmentId, string? recipient, MailDeliverySignal mail) : IAppointmentCheckInTransaction
{
    public CheckInContext Context => context;

    public async Task<DateTimeOffset> ReadDatabaseTimeAsync(CancellationToken cancellationToken)
    {
        return await database.Database.SqlQuery<DateTimeOffset>($"SELECT clock_timestamp() AS \"Value\"")
            .SingleAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CheckInPhotoCandidate>> LockPhotosAsync(CancellationToken cancellationToken)
    {
        var photos = await database.IntakePhotos.FromSqlInterpolated($"""
            SELECT p.* FROM "IntakePhotos" p JOIN "AppointmentPhotos" a ON a."PhotoId" = p."Id"
            WHERE a."AppointmentId" = {appointmentId} ORDER BY p."Id" FOR UPDATE OF p
            """).AsNoTracking().ToArrayAsync(cancellationToken);
        var photoIds = photos.Select(photo => photo.Id).ToArray();
        var removals = await database.PhotoRemovals.Where(item => photoIds.Contains(item.PhotoId))
            .Select(item => item.PhotoId).ToArrayAsync(cancellationToken);
        return [.. photos.Select(photo => new CheckInPhotoCandidate(photo.Id, photo.AvailableAt, removals.Contains(photo.Id)))];
    }

    public async Task<CheckInStatus> CommitAsync(Vehicle vehicle, AppointmentCheckIn checkIn, DateTimeOffset recordedAt,
        CancellationToken cancellationToken)
    {
        database.Vehicles.Add(vehicle);
        database.AppointmentCheckIns.Add(checkIn);
        database.AppointmentMailOutbox.Add(new AppointmentMail(appointmentId, AppointmentMailKind.CheckIn, recipient!, recordedAt));

        try
        {
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            mail.Request();
            return CheckInStatus.Succeeded;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        { ConstraintName: "IX_Vehicles_Vin" or "IX_Vehicles_RegistrationIdentity" })
        {
            return CheckInStatus.VehicleConflict;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        { ConstraintName: "CK_AppointmentCheckIns_Time" or "CK_AppointmentCheckIns_Photos" })
        {
            return CheckInStatus.Unavailable;
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
            // Disposal rolls back incomplete work; never retain an uncertain bundle in the scoped tracker.
            database.ChangeTracker.Clear();
        }
    }
}

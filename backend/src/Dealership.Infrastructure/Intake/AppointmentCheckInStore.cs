using Dealership.Application.Accounts;
using Dealership.Application.Intake;
using Dealership.Domain.Intake;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Dealership.Infrastructure.Intake;

public sealed class AppointmentCheckInStore(DealershipDbContext database, TimeProvider clock) : IAppointmentCheckInStore
{
    public Task<CheckInResult> CheckInAsync(Guid staffId, Guid appointmentId, CheckInEvidence evidence,
        CancellationToken cancellationToken)
    {
        return ExecuteAsync(staffId, appointmentId, evidence, null, cancellationToken);
    }

    public Task<CheckInResult> CorrectNoShowAsync(Guid staffId, Guid appointmentId, CheckInEvidence evidence,
        NoShowCorrectionRequest correction, CancellationToken cancellationToken)
    {
        return ExecuteAsync(staffId, appointmentId, evidence, correction, cancellationToken);
    }

    private async Task<CheckInResult> ExecuteAsync(Guid staffId, Guid appointmentId, CheckInEvidence evidence,
        NoShowCorrectionRequest? correction, CancellationToken cancellationToken)
    {
        if (appointmentId == Guid.Empty || staffId == Guid.Empty)
        {
            return new CheckInResult(CheckInStatus.Invalid);
        }

        try
        {
            evidence = evidence.Normalize();
            if (correction is not null)
            {
                correction = correction with
                {
                    Reason = AppointmentCheckIn.NormalizeCorrectionReason(correction.Reason),
                    AttendedAt = new DateTimeOffset(correction.AttendedAt.UtcTicks - correction.AttendedAt.UtcTicks % 10, TimeSpan.Zero)
                };
            }
        }
        catch (ArgumentException)
        {
            return new CheckInResult(CheckInStatus.Invalid);
        }

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            await database.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({"appointment:" + appointmentId}, 0))", cancellationToken);
            var appointment = await database.IntakeAppointments.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == appointmentId, cancellationToken);
            var ownerId = appointment?.OwnerId ?? staffId;
            var users = await database.Users.FromSqlInterpolated($"""
                SELECT * FROM "AspNetUsers" WHERE "Id" IN ({staffId}, {ownerId}) ORDER BY "Id" FOR SHARE
                """).AsNoTracking().ToArrayAsync(cancellationToken);
            var staff = users.SingleOrDefault(user => user.Id == staffId);

            if (staff is not { IsActive: true, BranchId: not null } ||
                staff.Role is not (AccountRoles.SalesAgent or AccountRoles.BranchManager) ||
                (correction is not null && staff.Role != AccountRoles.BranchManager))
            {
                return new CheckInResult(CheckInStatus.Forbidden);
            }

            var branchActive = await database.Branches.FromSqlInterpolated(
                $"SELECT * FROM \"Branches\" WHERE \"Id\" = {staff.BranchId} FOR UPDATE")
                .AnyAsync(branch => branch.IsActive, cancellationToken);

            if (!branchActive)
            {
                return new CheckInResult(CheckInStatus.Forbidden);
            }

            if (appointment is null || appointment.BranchId != staff.BranchId)
            {
                return new CheckInResult(CheckInStatus.NotFound);
            }

            var existing = await database.AppointmentCheckIns.AsNoTracking()
                .SingleOrDefaultAsync(item => item.AppointmentId == appointmentId, cancellationToken);

            if (existing is not null)
            {
                var sameCorrection = correction is null ? existing.CorrectionRecordedAt is null :
                    existing.CorrectionRecordedAt is not null && existing.CheckedInAt == correction.AttendedAt &&
                    existing.CorrectionReason == correction.Reason;
                return existing.Matches(evidence) && sameCorrection
                    ? new CheckInResult(CheckInStatus.Succeeded, Receipt(existing))
                    : new CheckInResult(CheckInStatus.Conflict);
            }

            var customer = users.Single(user => user.Id == ownerId);

            if (!customer.IsActive || !customer.EmailConfirmed || customer.Role != AccountRoles.Customer ||
                await database.AppointmentCancellations.AnyAsync(item => item.AppointmentId == appointmentId, cancellationToken))
            {
                return new CheckInResult(CheckInStatus.Unavailable);
            }

            var now = clock.GetUtcNow();
            if (correction is not null)
            {
                now = await database.Database.SqlQuery<DateTimeOffset>($"SELECT clock_timestamp() AS \"Value\"")
                    .SingleAsync(cancellationToken);
            }
            now = new DateTimeOffset(now.UtcTicks - now.UtcTicks % 10, TimeSpan.Zero);
            AppointmentCheckIn checkIn;

            try
            {
                if (correction is not null && now < AppointmentDay.EndsAt(appointment.StartsAt))
                {
                    return new CheckInResult(CheckInStatus.Unavailable);
                }

                checkIn = new AppointmentCheckIn(Guid.NewGuid(), appointment, staffId, evidence,
                    correction?.AttendedAt ?? now, correction is null ? null : now, correction?.Reason);
            }
            catch (ArgumentException)
            {
                return new CheckInResult(CheckInStatus.Unavailable);
            }

            var photos = await database.IntakePhotos.FromSqlInterpolated($"""
                SELECT p.* FROM "IntakePhotos" p JOIN "AppointmentPhotos" a ON a."PhotoId" = p."Id"
                WHERE a."AppointmentId" = {appointmentId} ORDER BY p."Id" FOR UPDATE OF p
                """).AsNoTracking().ToArrayAsync(cancellationToken);
            var photoIds = photos.Select(photo => photo.Id).ToArray();

            if (photos.Length != 4 || photos.Any(photo => photo.AvailableAt is null) ||
                await database.PhotoRemovals.AnyAsync(item => photoIds.Contains(item.PhotoId), cancellationToken))
            {
                return new CheckInResult(CheckInStatus.Unavailable);
            }

            database.Vehicles.Add(new Vehicle(appointment, checkIn));
            database.AppointmentCheckIns.Add(checkIn);
            database.AppointmentMailOutbox.Add(new AppointmentMail(appointmentId, AppointmentMailKind.CheckIn,
                customer.Email!, now));
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new CheckInResult(CheckInStatus.Succeeded, Receipt(checkIn));
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        { ConstraintName: "IX_Vehicles_Vin" or "IX_Vehicles_RegistrationIdentity" })
        {
            return new CheckInResult(CheckInStatus.VehicleConflict);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        { ConstraintName: "CK_AppointmentCheckIns_Time" or "CK_AppointmentCheckIns_Photos" })
        {
            return new CheckInResult(CheckInStatus.Unavailable);
        }
        finally
        {
            // Disposal rolls back incomplete work; never retain an uncertain bundle in the scoped tracker.
            database.ChangeTracker.Clear();
        }
    }

    private static CheckInReceipt Receipt(AppointmentCheckIn checkIn)
    {
        return new CheckInReceipt(checkIn.AppointmentId, checkIn.VehicleId, checkIn.StaffId, checkIn.CheckedInAt,
            new CheckInEvidence(checkIn.Vin, checkIn.MileageKilometres, checkIn.CustomerPresent,
                checkIn.IdentityConfirmed, checkIn.OwnershipConfirmed, checkIn.VehicleConfirmed, checkIn.Notes),
            checkIn.CorrectionRecordedAt is null ? null : new CorrectionReceipt(checkIn.CorrectionRecordedAt.Value, checkIn.CorrectionReason!));
    }
}

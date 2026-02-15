using System.Data;
using Dealership.Application.Intake;
using Dealership.Domain.Intake;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Intake;

public sealed class StaffAppointmentDetailStore(DealershipDbContext database) : IStaffAppointmentDetailStore
{
    public async Task<StaffAppointmentDetailResult> GetAsync(Guid staffId, Guid appointmentId,
        CancellationToken cancellationToken)
    {
        // Current authorization and detail must describe the same database snapshot.
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        var scope = await StaffIntakeReadScope.ResolveAsync(database, staffId, cancellationToken);

        if (scope is null)
        {
            return new StaffAppointmentDetailResult(StaffAppointmentDetailStatus.Forbidden);
        }

        var row = await database.IntakeAppointments.AsNoTracking()
            .Where(item => item.Id == appointmentId && (scope.BranchId == null || item.BranchId == scope.BranchId))
            .Join(database.Branches, item => item.BranchId, branch => branch.Id,
                (item, branch) => new { Appointment = item, BranchName = branch.Name })
            .Join(database.CustomerProfiles, row => row.Appointment.OwnerId, customer => customer.UserId,
                (row, customer) => new { row.Appointment, row.BranchName, CustomerName = customer.LegalName })
            .Select(row => new
            {
                row.Appointment.Id,
                row.Appointment.BranchId,
                row.BranchName,
                row.CustomerName,
                row.Appointment.RegistrationPlate,
                row.Appointment.StartsAt,
                row.Appointment.EndsAt,
                row.Appointment.Valuation.Make,
                row.Appointment.Valuation.Model,
                row.Appointment.Valuation.Trim,
                row.Appointment.Valuation.ManufactureYear,
                row.Appointment.Valuation.MileageKilometres,
                CheckedIn = database.AppointmentCheckIns.Any(checkIn => checkIn.AppointmentId == row.Appointment.Id),
                Cancelled = row.Appointment.CancelledAt != null,
                CheckIn = database.AppointmentCheckIns.SingleOrDefault(item => item.AppointmentId == row.Appointment.Id)
            }).SingleOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return new StaffAppointmentDetailResult(StaffAppointmentDetailStatus.NotFound);
        }

        var status = nameof(StaffWorklistStatus.Booked);

        if (row.Cancelled)
        {
            status = nameof(StaffWorklistStatus.Cancelled);
        }

        var databaseNow = await database.Database.SqlQuery<DateTimeOffset>($"SELECT clock_timestamp() AS \"Value\"")
            .SingleAsync(cancellationToken);
        var noShowAt = AppointmentDay.EndsAt(row.StartsAt);
        var wasNoShow = row.CheckIn?.CorrectionRecordedAt is not null ||
            (!row.CheckedIn && !row.Cancelled && databaseNow >= noShowAt);
        if (wasNoShow)
        {
            status = nameof(StaffWorklistStatus.NoShow);
        }

        if (row.CheckedIn)
        {
            status = nameof(StaffWorklistStatus.CheckedIn);
        }

        var detail = new StaffAppointmentDetail(row.Id, row.BranchId, row.BranchName, row.CustomerName,
            row.RegistrationPlate, row.StartsAt, row.EndsAt, status, row.Make, row.Model, row.Trim,
            row.ManufactureYear, row.MileageKilometres, wasNoShow ? noShowAt : null, row.CheckIn?.CorrectionRecordedAt, row.CheckIn?.CorrectionReason,
            row.CheckIn?.CorrectionRecordedAt is null ? null : row.CheckIn.StaffId);
        await transaction.CommitAsync(cancellationToken);
        return new StaffAppointmentDetailResult(StaffAppointmentDetailStatus.Succeeded, detail);
    }
}

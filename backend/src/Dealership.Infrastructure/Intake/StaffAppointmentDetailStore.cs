using Dealership.Domain.Common;
using System.Data;
using Dealership.Application.Accounts;
using Dealership.Application.Intake;
using Dealership.Domain.Intake;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Intake;

public sealed class StaffAppointmentDetailStore(DealershipDbContext database) : IStaffAppointmentDetailStore
{
    public async Task<AppointmentHandlersResult> HandlersAsync(Guid staffId, Guid appointmentId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        var scope = await StaffIntakeReadScope.ResolveAsync(database, staffId, cancellationToken);
        if (scope?.Role != AccountRoles.BranchManager)
        {
            return new(StaffAppointmentDetailStatus.Forbidden);
        }

        var branchId = await database.IntakeAppointments.AsNoTracking()
            .Where(item => item.Id == appointmentId && item.BranchId == scope.BranchId)
            .Select(item => (Guid?)item.BranchId).SingleOrDefaultAsync(cancellationToken);
        if (branchId is null)
        {
            return new(StaffAppointmentDetailStatus.NotFound);
        }

        var handlers = await database.Users.AsNoTracking().Where(user => user.IsActive && user.BranchId == branchId &&
                (user.Role == AccountRoles.SalesAgent || user.Role == AccountRoles.BranchManager))
            .OrderBy(user => user.Email).ThenBy(user => user.Id)
            .Select(user => new AppointmentHandler(user.Id, user.Email ?? string.Empty)).ToArrayAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(StaffAppointmentDetailStatus.Succeeded, handlers);
    }

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
                (row, customer) => new { row.Appointment, row.BranchName, CustomerName = customer.LegalName, customer.Mobile })
            .Select(row => new
            {
                row.Appointment.Id,
                row.Appointment.BranchId,
                row.BranchName,
                row.CustomerName,
                row.Mobile,
                CustomerEmail = database.Users.Where(user => user.Id == row.Appointment.OwnerId).Select(user => user.Email).FirstOrDefault(),
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

        var databaseNow = await database.DatabaseNowAsync(cancellationToken);
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

        var correctorId = row.CheckIn?.CorrectionRecordedAt is null ? (Guid?)null : row.CheckIn.StaffId;
        var correctorEmail = correctorId is null ? null
            : await database.Users.AsNoTracking().Where(user => user.Id == correctorId).Select(user => user.Email)
                .FirstOrDefaultAsync(cancellationToken);
        var assignment = await database.AppointmentAssignments.AsNoTracking()
            .Where(item => item.AppointmentId == appointmentId).OrderByDescending(item => item.Version)
            .FirstOrDefaultAsync(cancellationToken);
        var handler = assignment?.HandlerId is not { } handlerId ? null
            : await database.Users.AsNoTracking().Where(user => user.Id == handlerId)
                .Select(user => new { user.Email, user.IsActive, user.Role, user.BranchId })
                .SingleOrDefaultAsync(cancellationToken);
        var handlerEligible = handler is { IsActive: true } && handler.BranchId == row.BranchId &&
            handler.Role is AccountRoles.SalesAgent or AccountRoles.BranchManager &&
            await database.Branches.AnyAsync(branch => branch.Id == row.BranchId && branch.IsActive, cancellationToken);
        var ownership = assignment is null ? null
            : new AppointmentOwnership(assignment.Version, assignment.HandlerId, handler?.Email, handlerEligible,
                assignment.ActorId, assignment.Kind, assignment.RecordedAt, assignment.Reason);
        var detail = new StaffAppointmentDetail(row.Id, row.BranchId, row.BranchName, row.CustomerName,
            row.RegistrationPlate, row.StartsAt, row.EndsAt, status, row.Make, row.Model, row.Trim,
            row.ManufactureYear, row.MileageKilometres, wasNoShow ? noShowAt : null, row.CheckIn?.CorrectionRecordedAt, row.CheckIn?.CorrectionReason,
            correctorId, row.Mobile, row.CustomerEmail ?? string.Empty, correctorEmail, ownership);
        await transaction.CommitAsync(cancellationToken);
        return new StaffAppointmentDetailResult(StaffAppointmentDetailStatus.Succeeded, detail);
    }
}

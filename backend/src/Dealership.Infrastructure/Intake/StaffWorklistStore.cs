using System.Data;
using Dealership.Application.Accounts;
using Dealership.Application.Intake;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Intake;

public sealed class StaffWorklistStore(DealershipDbContext database) : IStaffWorklistStore
{
    public async Task<StaffWorklistPage?> ListAsync(Guid staffId, StaffWorklistQuery query,
        CancellationToken cancellationToken)
    {
        // Authorization, scoped count and rows must describe one snapshot.
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        var staff = await database.Users.AsNoTracking().SingleOrDefaultAsync(user => user.Id == staffId, cancellationToken);

        if (staff is not { IsActive: true } ||
            staff.Role is not (AccountRoles.SalesAgent or AccountRoles.BranchManager or AccountRoles.GeneralManager))
        {
            return null;
        }

        var branchId = query.BranchId;

        if (staff.Role != AccountRoles.GeneralManager)
        {
            if (staff.BranchId is null || (branchId is not null && branchId != staff.BranchId) ||
                !await database.Branches.AnyAsync(branch => branch.Id == staff.BranchId && branch.IsActive, cancellationToken))
            {
                return null;
            }

            branchId = staff.BranchId;
        }

        var rows = database.IntakeAppointments.AsNoTracking()
            .Where(item => item.StartsAt >= query.From && item.StartsAt < query.Until &&
                (branchId == null || item.BranchId == branchId))
            .Select(item => new
            {
                Appointment = item,
                CheckedIn = database.AppointmentCheckIns.Any(checkIn => checkIn.AppointmentId == item.Id),
                Cancelled = database.AppointmentCancellations.Any(cancellation => cancellation.AppointmentId == item.Id)
            });
        rows = query.Status switch
        {
            StaffWorklistStatus.Booked => rows.Where(row => !row.CheckedIn && !row.Cancelled),
            StaffWorklistStatus.CheckedIn => rows.Where(row => row.CheckedIn),
            StaffWorklistStatus.Cancelled => rows.Where(row => row.Cancelled),
            _ => rows
        };
        var total = await rows.CountAsync(cancellationToken);
        var offset = (long)(query.Page - 1) * query.PageSize;
        StaffWorklistItem[] items = [];

        if (offset < total)
        {
            var page = await rows.Join(database.Branches, row => row.Appointment.BranchId, branch => branch.Id,
                    (row, branch) => new { Row = row, BranchName = branch.Name })
                .Join(database.CustomerProfiles, row => row.Row.Appointment.OwnerId, customer => customer.UserId,
                    (row, customer) => new { row.Row, row.BranchName, CustomerName = customer.LegalName })
                .OrderBy(row => row.Row.Appointment.StartsAt).ThenBy(row => row.Row.Appointment.Id)
                .Skip((int)offset).Take(query.PageSize)
                .Select(row => new
                {
                    row.Row.Appointment.Id,
                    row.Row.Appointment.BranchId,
                    row.BranchName,
                    row.CustomerName,
                    row.Row.Appointment.RegistrationPlate,
                    row.Row.Appointment.StartsAt,
                    row.Row.Appointment.EndsAt,
                    row.Row.CheckedIn,
                    row.Row.Cancelled
                }).ToArrayAsync(cancellationToken);
            items = page.Select(row => new StaffWorklistItem(row.Id, row.BranchId, row.BranchName, row.CustomerName,
                row.RegistrationPlate, row.StartsAt, row.EndsAt, Status(row.CheckedIn, row.Cancelled))).ToArray();
        }

        await transaction.CommitAsync(cancellationToken);
        return new StaffWorklistPage(items, total, query.Page, query.PageSize);
    }

    private static string Status(bool checkedIn, bool cancelled)
    {
        if (checkedIn)
        {
            return nameof(StaffWorklistStatus.CheckedIn);
        }

        return cancelled ? nameof(StaffWorklistStatus.Cancelled) : nameof(StaffWorklistStatus.Booked);
    }
}

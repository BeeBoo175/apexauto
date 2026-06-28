using Dealership.Application.Accounts;
using Dealership.Domain.Sales;
using Dealership.Domain.TestDrives;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Accounts;

// The non-terminal records an employee currently owns, which must be resolved before their account is deactivated
// or moved to another branch. Unclaimed queue items and finished records never block.
internal static class StaffWorkBlockers
{
    public static async Task<IReadOnlyList<AccountBlocker>> ReadAsync(DealershipDbContext database, Guid staffId,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        var rows = new List<(string Type, string Reference, string Status, Guid BranchId)>();

        // Only the latest assignment names the current handler of an appointment or a test drive.
        rows.AddRange((await (from assignment in database.AppointmentAssignments.AsNoTracking()
                              join appointment in database.IntakeAppointments.AsNoTracking()
                                  on assignment.AppointmentId equals appointment.Id
                              where assignment.HandlerId == staffId && appointment.CancelledAt == null &&
                                  appointment.EndsAt > now &&
                                  assignment.Version == database.AppointmentAssignments
                                      .Where(other => other.AppointmentId == assignment.AppointmentId)
                                      .Max(other => other.Version) &&
                                  !database.AppointmentCheckIns.Any(checkIn => checkIn.AppointmentId == appointment.Id)
                              select new { appointment.RegistrationPlate, appointment.BranchId })
            .ToListAsync(cancellationToken)).Select(row => ("Intake appointment", row.RegistrationPlate, "Booked", row.BranchId)));

        rows.AddRange((await (from draft in database.InspectionDrafts.AsNoTracking()
                              where draft.TechnicianId == staffId && !draft.IsSigned
                              select new { draft.Id, draft.BranchId })
            .ToListAsync(cancellationToken)).Select(row => ("Workshop draft", ShortReference(row.Id), "Unsigned", row.BranchId)));

        // An approved repair request with no completion is repair work the technician still owns.
        rows.AddRange((await (from request in database.InspectionRepairRequests.AsNoTracking()
                              join draft in database.InspectionDrafts.AsNoTracking() on request.InspectionId equals draft.Id
                              where draft.TechnicianId == staffId &&
                                  database.InspectionRepairApprovals.Any(approval => approval.RequestId == request.Id) &&
                                  !database.InspectionRepairCompletions.Any(completion => completion.RequestId == request.Id)
                              select new { request.Id, request.BranchId })
            .ToListAsync(cancellationToken)).Select(row => ("Repair work", ShortReference(row.Id), "Approved", row.BranchId)));

        rows.AddRange((await (from sale in database.SalesOrders.AsNoTracking()
                              where sale.AgentId == staffId && (sale.Status == SalesOrderStatus.Handling ||
                                  sale.Status == SalesOrderStatus.PendingApproval || sale.Status == SalesOrderStatus.Approved ||
                                  sale.Status == SalesOrderStatus.Settled)
                              select new { sale.Id, sale.Status, sale.BranchId })
            .ToListAsync(cancellationToken)).Select(row =>
                ("Sale", ShortReference(row.Id), SaleStatusText(row.Status), row.BranchId)));

        rows.AddRange((await (from assignment in database.TestDriveAssignments.AsNoTracking()
                              join booking in database.TestDriveBookings.AsNoTracking()
                                  on assignment.BookingId equals booking.Id
                              where assignment.HandlerId == staffId &&
                                  assignment.Version == database.TestDriveAssignments
                                      .Where(other => other.BookingId == assignment.BookingId)
                                      .Max(other => other.Version) &&
                                  !database.TestDriveCancellations.Any(cancel => cancel.BookingId == booking.Id) &&
                                  !database.TestDriveOutcomes.Any(outcome => outcome.BookingId == booking.Id)
                              select new
                              {
                                  booking.Id,
                                  booking.BranchId,
                                  Started = database.TestDriveStarts.Any(start => start.BookingId == booking.Id)
                              })
            .ToListAsync(cancellationToken)).Select(row =>
                ("Test drive", ShortReference(row.Id), row.Started ? "In progress" : "Booked", row.BranchId)));

        if (rows.Count == 0)
        {
            return [];
        }

        var branchIds = rows.Select(row => row.BranchId).Distinct().ToArray();
        var branches = await database.Branches.AsNoTracking().Where(branch => branchIds.Contains(branch.Id))
            .ToDictionaryAsync(branch => branch.Id, branch => branch.Name, cancellationToken);
        var managers = (await (from user in database.Users.AsNoTracking()
                               where user.IsActive && user.Role == AccountRoles.BranchManager &&
                                   branchIds.Contains(user.BranchId!.Value)
                               join staff in database.StaffProfiles.AsNoTracking() on user.Id equals staff.UserId
                               orderby staff.Name
                               select new { BranchId = user.BranchId!.Value, staff.Name })
            .ToListAsync(cancellationToken)).GroupBy(item => item.BranchId)
            .ToDictionary(group => group.Key, group => string.Join(", ", group.Select(item => item.Name)));
        return [.. rows.Select(row => new AccountBlocker(row.Type, row.Reference, row.Status,
            branches.GetValueOrDefault(row.BranchId, string.Empty),
            managers.GetValueOrDefault(row.BranchId, "No active branch manager")))];
    }

    // A record reference an Administrator can quote to the managers without opening anything.
    private static string ShortReference(Guid id) => id.ToString("N")[..8].ToUpperInvariant();

    private static string SaleStatusText(SalesOrderStatus status) => status switch
    {
        SalesOrderStatus.PendingApproval => "Pending approval",
        SalesOrderStatus.Settled => "Awaiting delivery",
        _ => status.ToString()
    };
}

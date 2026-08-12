using Dealership.Domain.Sales;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Performance;

// Open work per person, shared by the workload and the team tables.
internal static class PerformanceWork
{
    // Upcoming booked appointments whose latest assignment names a handler.
    internal static async Task<List<(Guid, DateTimeOffset)>> IntakeWorkAsync(DealershipDbContext database,
        Guid? branchId, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var rows = await (from assignment in database.AppointmentAssignments.AsNoTracking()
                          join appointment in database.IntakeAppointments.AsNoTracking() on assignment.AppointmentId equals appointment.Id
                          where assignment.HandlerId != null && appointment.CancelledAt == null &&
                              appointment.EndsAt > now && (branchId == null || appointment.BranchId == branchId) &&
                              assignment.Version == database.AppointmentAssignments
                                  .Where(item => item.AppointmentId == appointment.Id).Max(item => item.Version) &&
                              !database.AppointmentCheckIns.Any(item => item.AppointmentId == appointment.Id)
                          select new { PersonId = assignment.HandlerId!.Value, Since = assignment.RecordedAt })
            .ToListAsync(cancellationToken);
        return [.. rows.Select(row => (row.PersonId, row.Since))];
    }

    // Test drives assigned to a handler that have neither finished nor been cancelled.
    internal static async Task<List<(Guid, DateTimeOffset)>> TestDriveWorkAsync(DealershipDbContext database,
        Guid? branchId,
        CancellationToken cancellationToken)
    {
        var rows = await (from assignment in database.TestDriveAssignments.AsNoTracking()
                          where assignment.HandlerId != null && (branchId == null || assignment.BranchId == branchId) &&
                              assignment.Version == database.TestDriveAssignments
                                  .Where(item => item.BookingId == assignment.BookingId).Max(item => item.Version) &&
                              !database.TestDriveOutcomes.Any(item => item.BookingId == assignment.BookingId) &&
                              !database.TestDriveCancellations.Any(item => item.BookingId == assignment.BookingId)
                          select new { PersonId = assignment.HandlerId!.Value, Since = assignment.RecordedAt })
            .ToListAsync(cancellationToken);
        return [.. rows.Select(row => (row.PersonId, row.Since))];
    }

    // Sales a person is handling, approved or has settled and not yet delivered.
    internal static async Task<List<(Guid, DateTimeOffset)>> SaleWorkAsync(DealershipDbContext database,
        Guid? branchId, CancellationToken cancellationToken)
    {
        var rows = await PerformanceScopes.SalesInScope(database, branchId)
            .Where(order => order.AgentId != null && order.ClaimedAt != null &&
                (order.Status == SalesOrderStatus.Handling || order.Status == SalesOrderStatus.PendingApproval ||
                    order.Status == SalesOrderStatus.Approved || order.Status == SalesOrderStatus.Settled))
            .Select(order => new { PersonId = order.AgentId!.Value, Since = order.ClaimedAt!.Value })
            .ToListAsync(cancellationToken);
        return [.. rows.Select(row => (row.PersonId, row.Since))];
    }

    // Unsigned drafts, aged from their latest assignment.
    internal static async Task<List<(Guid, DateTimeOffset)>> DraftWorkAsync(DealershipDbContext database,
        Guid? branchId, CancellationToken cancellationToken)
    {
        var drafts = await (from draft in database.InspectionDrafts.AsNoTracking()
                            join assignment in database.InspectionAssignments.AsNoTracking() on
                                new { InspectionId = draft.Id, Version = draft.AssignmentVersion } equals
                                new { assignment.InspectionId, assignment.Version }
                            where !draft.IsSigned && (branchId == null || draft.BranchId == branchId)
                            select new { PersonId = draft.TechnicianId, Since = assignment.RecordedAt })
            .ToListAsync(cancellationToken);
        return [.. drafts.Select(row => (row.PersonId, row.Since))];
    }

    // Signed drafts whose latest repair request is not yet completed, aged from that request.
    internal static async Task<List<(Guid, DateTimeOffset)>> RepairWorkAsync(DealershipDbContext database,
        Guid? branchId, CancellationToken cancellationToken)
    {
        var repairs = await (from draft in database.InspectionDrafts.AsNoTracking()
                             join request in database.InspectionRepairRequests.AsNoTracking() on
                                 new { InspectionId = draft.Id, Version = draft.RepairVersion } equals
                                 new { request.InspectionId, request.Version }
                             where draft.IsSigned && draft.RepairVersion > 0 && (branchId == null || draft.BranchId == branchId) &&
                                 !database.InspectionRepairCompletions.Any(completion =>
                                     completion.InspectionId == draft.Id && completion.RepairVersion == draft.RepairVersion)
                             select new { PersonId = draft.TechnicianId, Since = request.RequestedAt })
            .ToListAsync(cancellationToken);
        return [.. repairs.Select(row => (row.PersonId, row.Since))];
    }
}

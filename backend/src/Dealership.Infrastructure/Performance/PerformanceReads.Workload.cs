using System.Data;
using Dealership.Application.Accounts;
using Dealership.Application.Performance;
using Dealership.Domain.Sales;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Performance;

public sealed partial class PerformanceReads
{
    private static readonly string[] WorkloadRoles =
        [AccountRoles.SalesAgent, AccountRoles.BranchManager, AccountRoles.WorkshopTechnician];

    // Open work now, held by people who are currently in scope. Age runs from the current holder's assignment, so a
    // reassigned item starts again for its new holder.
    public async Task<PerformanceReadResult<PerformanceWorkload>> WorkloadAsync(Guid actorId, PerformanceQuery query,
        CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead,
            cancellationToken);
        var scope = await ResolveScopeAsync(actorId, query, cancellationToken);
        if (scope is null)
        {
            return new(PerformanceReadStatus.Forbidden);
        }

        var now = await database.DatabaseNowAsync(cancellationToken);
        var branchId = scope.BranchId;
        var people = await (from user in database.Users.AsNoTracking()
                            join branch in database.Branches.AsNoTracking() on user.BranchId equals branch.Id
                            join profile in database.StaffProfiles.AsNoTracking() on user.Id equals profile.UserId
                            where user.IsActive && WorkloadRoles.Contains(user.Role) &&
                                (branchId == null || user.BranchId == branchId)
                            select new { user.Id, profile.Name, user.Role, BranchId = branch.Id, BranchName = branch.Name })
            .ToListAsync(cancellationToken);

        var started = new List<(Guid PersonId, DateTimeOffset Since)>();
        started.AddRange(await IntakeWorkAsync(branchId, now, cancellationToken));
        started.AddRange(await TestDriveWorkAsync(branchId, cancellationToken));
        started.AddRange(await SaleWorkAsync(branchId, cancellationToken));
        started.AddRange(await DraftWorkAsync(branchId, cancellationToken));
        started.AddRange(await RepairWorkAsync(branchId, cancellationToken));
        await transaction.CommitAsync(cancellationToken);

        var rows = people.OrderBy(person => person.BranchName).ThenBy(person => person.Name).ThenBy(person => person.Id)
            .Select(person =>
            {
                var ages = started.Where(item => item.PersonId == person.Id).Select(item => now - item.Since).ToArray();
                return new PerformanceWorkloadPerson(person.Id, person.Name, person.Role, person.BranchId,
                    person.BranchName, ages.Count(age => age < TimeSpan.FromDays(2)),
                    ages.Count(age => age >= TimeSpan.FromDays(2) && age <= TimeSpan.FromDays(7)),
                    ages.Count(age => age > TimeSpan.FromDays(7)));
            }).ToArray();
        return new(PerformanceReadStatus.Succeeded, new PerformanceWorkload(scope.Actor, rows));
    }

    // Upcoming booked appointments whose latest assignment names a handler.
    private async Task<List<(Guid, DateTimeOffset)>> IntakeWorkAsync(Guid? branchId, DateTimeOffset now,
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
        return rows.Select(row => (row.PersonId, row.Since)).ToList();
    }

    // Test drives assigned to a handler that have neither finished nor been cancelled.
    private async Task<List<(Guid, DateTimeOffset)>> TestDriveWorkAsync(Guid? branchId,
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
        return rows.Select(row => (row.PersonId, row.Since)).ToList();
    }

    // Sales a person is handling, approved or has settled and not yet delivered.
    private async Task<List<(Guid, DateTimeOffset)>> SaleWorkAsync(Guid? branchId, CancellationToken cancellationToken)
    {
        var rows = await SalesInScope(branchId)
            .Where(order => order.AgentId != null && order.ClaimedAt != null &&
                (order.Status == SalesOrderStatus.Handling || order.Status == SalesOrderStatus.PendingApproval ||
                    order.Status == SalesOrderStatus.Approved || order.Status == SalesOrderStatus.Settled))
            .Select(order => new { PersonId = order.AgentId!.Value, Since = order.ClaimedAt!.Value })
            .ToListAsync(cancellationToken);
        return rows.Select(row => (row.PersonId, row.Since)).ToList();
    }

    // Unsigned drafts, aged from their latest assignment.
    private async Task<List<(Guid, DateTimeOffset)>> DraftWorkAsync(Guid? branchId, CancellationToken cancellationToken)
    {
        var drafts = await (from draft in database.InspectionDrafts.AsNoTracking()
                            join assignment in database.InspectionAssignments.AsNoTracking() on
                                new { InspectionId = draft.Id, Version = draft.AssignmentVersion } equals
                                new { assignment.InspectionId, assignment.Version }
                            where !draft.IsSigned && (branchId == null || draft.BranchId == branchId)
                            select new { PersonId = draft.TechnicianId, Since = assignment.RecordedAt })
            .ToListAsync(cancellationToken);
        return drafts.Select(row => (row.PersonId, row.Since)).ToList();
    }

    // Signed drafts whose latest repair request is not yet completed, aged from that request.
    private async Task<List<(Guid, DateTimeOffset)>> RepairWorkAsync(Guid? branchId, CancellationToken cancellationToken)
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
        return repairs.Select(row => (row.PersonId, row.Since)).ToList();
    }
}

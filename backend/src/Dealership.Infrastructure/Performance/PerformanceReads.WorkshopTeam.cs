using System.Data;
using Dealership.Application.Accounts;
using Dealership.Application.Performance;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Performance;

public sealed partial class PerformanceReads
{
    private static readonly string[] WorkshopTeamRoles = [AccountRoles.WorkshopTechnician];

    // Workshop: every technician in scope appears, with first inspections and re-inspections kept apart.
    public async Task<PerformanceReadResult<PerformanceTeamPage<PerformanceWorkshopRow>>> WorkshopTeamAsync(
        Guid actorId, PerformanceTeamQuery team, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead,
            cancellationToken);
        var scope = await ResolveScopeAsync(actorId, team.Period, cancellationToken);
        if (scope is null)
        {
            return new(PerformanceReadStatus.Forbidden);
        }

        var query = team.Period;
        var branchId = scope.BranchId;
        var people = await TeamPeople.ReadAsync(database, WorkshopTeamRoles, cancellationToken);
        var signed = await (from item in database.InspectionSignedRevisions.AsNoTracking()
                            join assignment in database.InspectionAssignments.AsNoTracking() on
                                new { item.InspectionId, Version = item.AssignmentVersion } equals
                                new { assignment.InspectionId, assignment.Version }
                            where item.SignedAt >= query.From && item.SignedAt < query.Until &&
                                (branchId == null || item.BranchId == branchId)
                            select new
                            {
                                item.TechnicianId,
                                item.BranchId,
                                item.Revision,
                                item.SignedAt,
                                Assigned = assignment.RecordedAt,
                                Reopened = database.InspectionReopenings
                                    .Where(reopening => reopening.InspectionId == item.InspectionId &&
                                        reopening.Revision == item.Revision)
                                    .Select(reopening => (DateTimeOffset?)reopening.RecordedAt).FirstOrDefault()
                            }).ToListAsync(cancellationToken);
        var repairs = await database.InspectionRepairCompletions.AsNoTracking()
            .Where(item => item.CompletedAt >= query.From && item.CompletedAt < query.Until &&
                (branchId == null || item.BranchId == branchId))
            .GroupBy(item => new { item.ActorId, item.BranchId })
            .Select(group => new { group.Key.ActorId, group.Key.BranchId, Count = group.Count() })
            .ToListAsync(cancellationToken);
        var drafts = await DraftWorkAsync(branchId, cancellationToken);
        var openRepairs = await RepairWorkAsync(branchId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var keys = signed.Select(item => (item.TechnicianId, item.BranchId))
            .Concat(repairs.Select(item => (item.ActorId, item.BranchId)))
            .Where(key => people.Includes(key.Item1))
            .Concat(people.Current(AccountRoles.WorkshopTechnician, branchId))
            .Distinct()
            .ToArray();
        var rows = keys.Select(key =>
        {
            var (personId, rowBranch) = key;
            var mine = signed.Where(item => item.TechnicianId == personId && item.BranchId == rowBranch).ToArray();
            var first = mine.Where(item => item.Revision == 1).ToArray();
            var again = mine.Where(item => item.Revision > 1).ToArray();
            var current = people.IsCurrent(personId, rowBranch);
            return new PerformanceWorkshopRow(personId, people.Name(personId), people.Role(personId), rowBranch,
                people.BranchName(rowBranch), people.Picture(personId), people.Status(personId, rowBranch), first.Length, again.Length,
                repairs.Where(item => item.ActorId == personId && item.BranchId == rowBranch).Sum(item => item.Count),
                AverageDays(first.Select(item => (item.SignedAt - item.Assigned).TotalSeconds)),
                AverageDays(again.Select(item => (item.SignedAt - Later(item.Assigned, item.Reopened)).TotalSeconds)),
                current ? drafts.Count(item => item.Item1 == personId) : null,
                current ? openRepairs.Count(item => item.Item1 == personId) : null);
        });
        return new(PerformanceReadStatus.Succeeded, PageOf(scope, team,
            rows.OrderBy(row => row.BranchName).ThenBy(row => row.Name).ThenBy(row => row.PersonId),
            row => new PerformanceTeamPoint(row.PersonId, row.BranchId, row.Name, row.InspectionsSigned,
                row.AssignmentToSignOffDays)));
    }

    private static DateTimeOffset Later(DateTimeOffset assigned, DateTimeOffset? reopened)
    {
        return reopened is { } at && at > assigned ? at : assigned;
    }
}

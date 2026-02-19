using System.Data;
using Dealership.Application.Accounts;
using Dealership.Application.Inspections;
using Dealership.Domain.Inspections;
using Dealership.Infrastructure.Identity;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Inspections;

public sealed class InspectionHistoryStore(DealershipDbContext database) : IInspectionHistoryStore
{
    public async Task<InspectionRevisionHistoryResult> RevisionsAsync(Guid actorId, Guid inspectionId,
        InspectionHistoryQuery query, CancellationToken cancellationToken)
    {
        if (inspectionId == Guid.Empty || !query.IsValid)
        {
            return new(InspectionAssignmentStatus.Invalid);
        }

        // Scoped totals, immutable rows and current draft state describe one database snapshot.
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        var (status, draft) = await LockScopeAsync(actorId, inspectionId, cancellationToken);
        if (draft is null)
        {
            return new(status);
        }

        var revisions = database.InspectionSignedRevisions.AsNoTracking().Where(item => item.InspectionId == inspectionId);
        var total = await revisions.CountAsync(cancellationToken);
        var offset = (long)(query.Page - 1) * query.PageSize;
        InspectionRevisionSummary[] items = [];
        if (offset < total)
        {
            var rows = await (from signed in revisions
                              join reopened in database.InspectionReopenings.AsNoTracking() on signed.Id equals reopened.SignedRevisionId into reopenings
                              from reopening in reopenings.DefaultIfEmpty()
                              orderby signed.Revision descending
                              select new { Signed = signed, Reopening = reopening })
                .Skip((int)offset).Take(query.PageSize).ToArrayAsync(cancellationToken);
            items = [.. rows.Select(row => Summary(row.Signed.Report(), row.Reopening?.Receipt()))];
        }

        await transaction.CommitAsync(cancellationToken);
        return new(InspectionAssignmentStatus.Succeeded,
            new(inspectionId, draft.Revision, draft.IsSigned, Array.AsReadOnly(items), total, query.Page, query.PageSize));
    }

    public async Task<InspectionRepairHistoryResult> RepairsAsync(Guid actorId, Guid inspectionId, long revision,
        InspectionHistoryQuery query, CancellationToken cancellationToken)
    {
        if (inspectionId == Guid.Empty || revision < 1 || !query.IsValid)
        {
            return new(InspectionAssignmentStatus.Invalid);
        }

        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        var (status, draft) = await LockScopeAsync(actorId, inspectionId, cancellationToken);
        if (draft is null)
        {
            return new(status);
        }

        if (!await database.InspectionSignedRevisions.AnyAsync(item => item.InspectionId == inspectionId && item.Revision == revision, cancellationToken))
        {
            return new(InspectionAssignmentStatus.NotFound);
        }

        var requests = database.InspectionRepairRequests.AsNoTracking()
            .Where(item => item.InspectionId == inspectionId && item.Revision == revision);
        var total = await requests.CountAsync(cancellationToken);
        var offset = (long)(query.Page - 1) * query.PageSize;
        InspectionRepairHistoryEntry[] items = [];
        if (offset < total)
        {
            var rows = await (from request in requests
                              join approved in database.InspectionRepairApprovals.AsNoTracking() on request.Id equals approved.RequestId into approvals
                              from approval in approvals.DefaultIfEmpty()
                              orderby request.Version descending
                              select new { Request = request, Approval = approval })
                .Skip((int)offset).Take(query.PageSize).ToArrayAsync(cancellationToken);
            items = [.. rows.Select(row => new InspectionRepairHistoryEntry(row.Request.Receipt(), row.Approval?.Receipt()))];
        }

        await transaction.CommitAsync(cancellationToken);
        return new(InspectionAssignmentStatus.Succeeded,
            new(inspectionId, revision, Array.AsReadOnly(items), total, query.Page, query.PageSize));
    }

    private static InspectionRevisionSummary Summary(InspectionReport report, InspectionReopenReceipt? reopening) =>
        new(report.OperationId, report.Revision, report.SaveVersion, report.AssignmentVersion, report.TechnicianId,
            report.TechnicianIdentity, report.CatalogVersion, report.Assessment, report.SignedAt, reopening);

    private async Task<(InspectionAssignmentStatus Status, InspectionDraft? Draft)> LockScopeAsync(Guid actorId,
        Guid inspectionId, CancellationToken cancellationToken)
    {
        var actor = await database.Users.FromSqlInterpolated(
            $"SELECT * FROM \"AspNetUsers\" WHERE \"Id\" = {actorId} FOR SHARE")
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (actor is not { IsActive: true } ||
            actor.Role is not (AccountRoles.WorkshopTechnician or AccountRoles.BranchManager or AccountRoles.GeneralManager))
        {
            return (InspectionAssignmentStatus.Forbidden, null);
        }

        if (actor.Role != AccountRoles.GeneralManager && (actor.BranchId is null ||
            !await database.Branches.FromSqlInterpolated($"SELECT * FROM \"Branches\" WHERE \"Id\" = {actor.BranchId} FOR SHARE")
                .AnyAsync(branch => branch.IsActive, cancellationToken)))
        {
            return (InspectionAssignmentStatus.Forbidden, null);
        }

        var draft = await database.InspectionDrafts.FromSqlInterpolated(
            $"SELECT * FROM \"InspectionDrafts\" WHERE \"Id\" = {inspectionId} FOR SHARE")
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (draft is null || actor.Role != AccountRoles.GeneralManager && draft.BranchId != actor.BranchId ||
            actor.Role == AccountRoles.WorkshopTechnician && draft.TechnicianId != actorId)
        {
            return (InspectionAssignmentStatus.NotFound, null);
        }

        return (InspectionAssignmentStatus.Succeeded, draft);
    }
}

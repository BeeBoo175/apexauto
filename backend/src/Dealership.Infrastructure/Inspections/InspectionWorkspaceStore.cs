using System.Data;
using Dealership.Application.Accounts;
using Dealership.Application.Inspections;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Inspections;

public sealed class InspectionWorkspaceStore(DealershipDbContext database) : IInspectionWorkspaceStore
{
    public async Task<InspectionWorkspaceResult> ReadAsync(Guid actorId, Guid inspectionId,
        CancellationToken cancellationToken)
    {
        if (inspectionId == Guid.Empty)
        {
            return new(InspectionAssignmentStatus.Invalid);
        }

        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        var actor = await InspectionReadScope.LockActorAsync(database, actorId, cancellationToken);
        if (actor is null)
        {
            return new(InspectionAssignmentStatus.Forbidden);
        }

        var drafts = database.InspectionDrafts.FromSqlInterpolated(
            $"SELECT * FROM \"InspectionDrafts\" WHERE \"Id\" = {inspectionId} FOR SHARE").AsNoTracking();
        if (actor.Role != AccountRoles.GeneralManager)
        {
            drafts = drafts.Where(draft => draft.BranchId == actor.BranchId);
        }
        if (actor.Role == AccountRoles.WorkshopTechnician)
        {
            drafts = drafts.Where(draft => draft.TechnicianId == actorId);
        }

        var draft = await InspectionDraftProjection.ReadAsync(database, drafts, inspectionId, cancellationToken);
        if (draft is null)
        {
            return new(InspectionAssignmentStatus.NotFound);
        }

        InspectionReport? report = null;
        InspectionRepairReceipt? request = null;
        InspectionRepairQueueItem? state = null;
        if (draft.IsSigned)
        {
            var signed = await database.InspectionSignedRevisions.AsNoTracking()
                .SingleAsync(item => item.InspectionId == inspectionId && item.Revision == draft.Revision, cancellationToken);
            report = signed.Report();
            // Repair versions span revisions; a newly signed revision can have no request yet.
            var requests = database.InspectionRepairRequests.AsNoTracking().Where(item =>
                item.InspectionId == inspectionId && item.Revision == draft.Revision && item.Version == draft.RepairVersion);
            var row = await InspectionRepairProjection.Read(database, requests, null).SingleOrDefaultAsync(cancellationToken);
            request = row?.Request.Receipt();
            state = row is null ? null : InspectionRepairProjection.Item(row);
        }

        await transaction.CommitAsync(cancellationToken);
        return new(InspectionAssignmentStatus.Succeeded, new(draft, report, request, state));
    }
}

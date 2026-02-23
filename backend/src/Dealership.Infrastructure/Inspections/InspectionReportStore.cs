using Dealership.Application.Accounts;
using Dealership.Application.Inspections;
using Dealership.Infrastructure.Identity;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Inspections;

public sealed class InspectionReportStore(DealershipDbContext database) : IInspectionReportStore
{
    public async Task<InspectionReportResult> ReadAsync(Guid actorId, Guid inspectionId, long revision,
        CancellationToken cancellationToken)
    {
        if (inspectionId == Guid.Empty || revision < 1)
        {
            return new(InspectionAssignmentStatus.Invalid);
        }

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var actor = await LockActorAsync(actorId, cancellationToken);
        if (actor is null)
        {
            return new(InspectionAssignmentStatus.Forbidden);
        }

        var drafts = database.InspectionDrafts.AsNoTracking();
        if (actor.Role != AccountRoles.GeneralManager)
        {
            drafts = drafts.Where(draft => draft.BranchId == actor.BranchId);
        }
        if (actor.Role == AccountRoles.WorkshopTechnician)
        {
            drafts = drafts.Where(draft => draft.TechnicianId == actorId);
        }

        var signed = await database.InspectionSignedRevisions.AsNoTracking()
            .Where(item => item.InspectionId == inspectionId && item.Revision == revision)
            .Join(drafts, item => item.InspectionId, draft => draft.Id, (item, _) => item)
            .SingleOrDefaultAsync(cancellationToken);
        return signed is null ? new(InspectionAssignmentStatus.NotFound)
            : new(InspectionAssignmentStatus.Succeeded, signed.Report());
    }

    private async Task<AccountUser?> LockActorAsync(Guid actorId, CancellationToken cancellationToken)
    {
        var actor = await database.Users.FromSqlInterpolated(
            $"SELECT * FROM \"AspNetUsers\" WHERE \"Id\" = {actorId} FOR SHARE")
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (actor is not { IsActive: true } ||
            actor.Role is not (AccountRoles.WorkshopTechnician or AccountRoles.BranchManager or AccountRoles.GeneralManager))
        {
            return null;
        }

        if (actor.Role != AccountRoles.GeneralManager && (actor.BranchId is null ||
            !await database.Branches.FromSqlInterpolated($"SELECT * FROM \"Branches\" WHERE \"Id\" = {actor.BranchId} FOR SHARE")
                .AnyAsync(branch => branch.IsActive, cancellationToken)))
        {
            return null;
        }
        return actor;
    }
}

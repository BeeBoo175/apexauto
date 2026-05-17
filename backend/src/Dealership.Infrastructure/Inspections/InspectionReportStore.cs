using Dealership.Application.Inspections;
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
        var actor = await InspectionReadScope.LockActorAsync(database, actorId, cancellationToken, allowCustomer: true);
        if (actor is null)
        {
            return new(InspectionAssignmentStatus.Forbidden);
        }

        var drafts = InspectionReadScope.ReportDrafts(database, actor);

        var signed = await database.InspectionSignedRevisions.AsNoTracking()
            .Where(item => item.InspectionId == inspectionId && item.Revision == revision)
            .Join(drafts, item => item.InspectionId, draft => draft.Id, (item, _) => item)
            .SingleOrDefaultAsync(cancellationToken);
        return signed is null ? new(InspectionAssignmentStatus.NotFound)
            : new(InspectionAssignmentStatus.Succeeded, signed.Report());
    }
}

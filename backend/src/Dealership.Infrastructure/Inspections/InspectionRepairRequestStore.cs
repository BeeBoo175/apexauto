using Dealership.Application.Accounts;
using Dealership.Application.Inspections;
using Dealership.Infrastructure.Mail;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Inspections;

public sealed class InspectionRepairRequestStore(DealershipDbContext database, MailDeliverySignal mail) : IInspectionRepairRequestStore
{
    public async Task<IInspectionRepairRequestTransaction> LockAsync(Guid actorId, Guid inspectionId, Guid operationId,
        CancellationToken cancellationToken)
    {
        var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await database.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({"inspection-repair-request:" + operationId}, 0))", cancellationToken);
            var rows = await InspectionCommandLocks.LockAsync(database, actorId, inspectionId, cancellationToken);
            var existing = await database.InspectionRepairRequests.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == operationId, cancellationToken);
            var context = new InspectionRepairRequestContext(
                rows.Actor,
                rows.BranchActive, rows.Vehicle, rows.Draft, existing?.Receipt());
            return new InspectionRepairRequestTransaction(database, transaction, mail, context);
        }
        catch
        {
            try
            {
                await transaction.DisposeAsync();
            }
            finally
            {
                database.ChangeTracker.Clear();
            }

            throw;
        }
    }

    public async Task<InspectionRepairResult> ReadAsync(Guid actorId, Guid inspectionId, long revision,
        CancellationToken cancellationToken)
    {
        if (inspectionId == Guid.Empty || revision < 1)
        {
            return new(InspectionAssignmentStatus.Invalid);
        }

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var actor = await InspectionReadScope.LockActorAsync(database, actorId, cancellationToken);
        if (actor is null)
        {
            return new(InspectionAssignmentStatus.Forbidden);
        }

        var draft = await database.InspectionDrafts.FromSqlInterpolated(
            $"SELECT * FROM \"InspectionDrafts\" WHERE \"Id\" = {inspectionId} FOR SHARE")
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (draft is null || actor.Role != AccountRoles.GeneralManager && draft.BranchId != actor.BranchId ||
            actor.Role == AccountRoles.WorkshopTechnician && draft.TechnicianId != actorId)
        {
            return new(InspectionAssignmentStatus.NotFound);
        }

        var latest = await database.InspectionRepairRequests.AsNoTracking()
            .Where(item => item.InspectionId == inspectionId && item.Revision == revision)
            .OrderByDescending(item => item.Version).FirstOrDefaultAsync(cancellationToken);
        return latest is null ? new(InspectionAssignmentStatus.NotFound)
            : new(InspectionAssignmentStatus.Succeeded, latest.Receipt());
    }
}

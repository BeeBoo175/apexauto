using Dealership.Application.Inspections;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Inspections;

public sealed class InspectionDraftSaveStore(DealershipDbContext database) : IInspectionDraftSaveStore
{
    public async Task<IInspectionDraftSaveTransaction> LockAsync(Guid actorId, Guid inspectionId, Guid operationId,
        CancellationToken cancellationToken)
    {
        var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            await database.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({"inspection-save:" + operationId}, 0))", cancellationToken);
            var rows = await InspectionCommandLocks.LockAsync(database, actorId, inspectionId, cancellationToken);
            var existing = await database.InspectionDraftSaves.AsNoTracking()
                .SingleOrDefaultAsync(save => save.Id == operationId, cancellationToken);
            var version = await database.InspectionDraftSaves.Where(save => save.InspectionId == inspectionId)
                .Select(save => (long?)save.Version).MaxAsync(cancellationToken) ?? 0;
            var context = new InspectionDraftSaveContext(
                rows.Actor,
                rows.BranchActive, rows.Vehicle, rows.Draft, existing?.Receipt(), version);
            return new InspectionDraftSaveTransaction(database, transaction, context);
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
}

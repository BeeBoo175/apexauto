using Dealership.Application.Inspections;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Inspections;

public sealed class InspectionRepairCompletionStore(DealershipDbContext database) : IInspectionRepairCompletionStore
{
    public async Task<IInspectionRepairCompletionTransaction> LockAsync(Guid actorId, Guid inspectionId, Guid operationId,
        CancellationToken cancellationToken)
    {
        var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await database.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({"inspection-repair-completion:" + operationId}, 0))", cancellationToken);
            var rows = await InspectionCommandLocks.LockAsync(database, actorId, inspectionId, cancellationToken);
            var existing = await database.InspectionRepairCompletions.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == operationId, cancellationToken);
            var context = new InspectionRepairCompletionContext(
                rows.Actor,
                rows.BranchActive, rows.Vehicle, rows.Draft, existing?.Receipt());
            return new InspectionRepairCompletionTransaction(database, transaction, context);
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

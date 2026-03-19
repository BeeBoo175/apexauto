using Dealership.Application.Inspections;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Inspections;

public sealed class InspectionSignoffStore(DealershipDbContext database) : IInspectionSignoffStore
{
    public async Task<IInspectionSignoffTransaction> LockAsync(Guid actorId, Guid inspectionId, Guid operationId,
        CancellationToken cancellationToken)
    {
        var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await database.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({"inspection-sign:" + operationId}, 0))", cancellationToken);
            var rows = await InspectionCommandLocks.LockAsync(database, actorId, inspectionId, cancellationToken);
            var existing = await database.InspectionSignedRevisions.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == operationId, cancellationToken);
            var context = new InspectionSignoffContext(
                rows.Actor,
                rows.BranchActive, rows.TechnicianIdentity, rows.Vehicle, rows.Draft, existing?.Report());
            return new InspectionSignoffTransaction(database, transaction, context);
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

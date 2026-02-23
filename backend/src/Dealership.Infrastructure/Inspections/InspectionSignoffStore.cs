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
            var actor = await database.Users.FromSqlInterpolated(
                $"SELECT * FROM \"AspNetUsers\" WHERE \"Id\" = {actorId} FOR SHARE")
                .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
            var branchActive = false;
            if (actor?.BranchId is not null)
            {
                branchActive = await database.Branches.FromSqlInterpolated(
                    $"SELECT * FROM \"Branches\" WHERE \"Id\" = {actor.BranchId} FOR SHARE")
                    .AnyAsync(branch => branch.IsActive, cancellationToken);
            }

            var vehicleId = await database.InspectionDrafts.Where(draft => draft.Id == inspectionId)
                .Select(draft => (Guid?)draft.VehicleId).SingleOrDefaultAsync(cancellationToken);
            var vehicle = await database.Vehicles.FromSqlInterpolated(
                $"SELECT * FROM \"Vehicles\" WHERE \"Id\" = {vehicleId} FOR UPDATE")
                .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
            var draft = await database.InspectionDrafts.FromSqlInterpolated(
                $"SELECT * FROM \"InspectionDrafts\" WHERE \"Id\" = {inspectionId} FOR UPDATE")
                .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
            var existing = await database.InspectionSignedRevisions.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == operationId, cancellationToken);
            var context = new InspectionSignoffContext(
                actor is null ? null : new InspectionStaff(actor.IsActive, actor.Role, actor.BranchId),
                branchActive, actor?.UserName, vehicle, draft, existing?.Report());
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

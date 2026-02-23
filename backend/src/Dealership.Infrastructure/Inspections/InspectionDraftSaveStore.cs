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

            // Assignment and save commands share vehicle-before-draft lock order.
            var vehicleId = await database.InspectionDrafts.Where(draft => draft.Id == inspectionId)
                .Select(draft => (Guid?)draft.VehicleId).SingleOrDefaultAsync(cancellationToken);
            var vehicle = await database.Vehicles.FromSqlInterpolated(
                $"SELECT * FROM \"Vehicles\" WHERE \"Id\" = {vehicleId} FOR UPDATE")
                .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
            var draft = await database.InspectionDrafts.FromSqlInterpolated(
                $"SELECT * FROM \"InspectionDrafts\" WHERE \"Id\" = {inspectionId} FOR UPDATE")
                .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
            var existing = await database.InspectionDraftSaves.AsNoTracking()
                .SingleOrDefaultAsync(save => save.Id == operationId, cancellationToken);
            var version = await database.InspectionDraftSaves.Where(save => save.InspectionId == inspectionId)
                .Select(save => (long?)save.Version).MaxAsync(cancellationToken) ?? 0;
            var context = new InspectionDraftSaveContext(
                actor is null ? null : new InspectionStaff(actor.IsActive, actor.Role, actor.BranchId),
                branchActive, vehicle, draft, existing?.Receipt(), version);
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

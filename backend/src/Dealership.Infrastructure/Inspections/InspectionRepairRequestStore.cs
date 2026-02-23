using Dealership.Application.Accounts;
using Dealership.Application.Inspections;
using Dealership.Infrastructure.Identity;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Inspections;

public sealed class InspectionRepairRequestStore(DealershipDbContext database) : IInspectionRepairRequestStore
{
    public async Task<IInspectionRepairRequestTransaction> LockAsync(Guid actorId, Guid inspectionId, Guid operationId,
        CancellationToken cancellationToken)
    {
        var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await database.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({"inspection-repair-request:" + operationId}, 0))", cancellationToken);
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
            var existing = await database.InspectionRepairRequests.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == operationId, cancellationToken);
            var context = new InspectionRepairRequestContext(
                actor is null ? null : new InspectionStaff(actor.IsActive, actor.Role, actor.BranchId),
                branchActive, vehicle, draft, existing?.Receipt());
            return new InspectionRepairRequestTransaction(database, transaction, context);
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
        var actor = await LockActorAsync(actorId, cancellationToken);
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

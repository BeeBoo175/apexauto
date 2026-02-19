using Dealership.Application.Accounts;
using Dealership.Application.Inspections;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Identity;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Dealership.Infrastructure.Inspections;

public sealed class InspectionRepairApprovalStore(DealershipDbContext database) : IInspectionRepairApprovalStore
{
    public async Task<InspectionRepairApprovalResult> ApproveAsync(Guid actorId, Guid inspectionId,
        InspectionRepairApprovalRequest request, CancellationToken cancellationToken)
    {
        if (actorId == Guid.Empty || inspectionId == Guid.Empty || request.OperationId == Guid.Empty ||
            request.RequestId == Guid.Empty || request.AssignmentVersion < 1 || request.Revision < 1 || request.RepairVersion < 1)
        {
            return new(InspectionAssignmentStatus.Invalid);
        }

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await database.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({"inspection-repair-approval:" + request.OperationId}, 0))", cancellationToken);
            var actor = await LockActorAsync(actorId, cancellationToken);
            if (actor is not { Role: AccountRoles.BranchManager })
            {
                return new(InspectionAssignmentStatus.Forbidden);
            }

            var vehicleId = await database.InspectionDrafts.Where(draft => draft.Id == inspectionId)
                .Select(draft => (Guid?)draft.VehicleId).SingleOrDefaultAsync(cancellationToken);
            var vehicle = await database.Vehicles.FromSqlInterpolated(
                $"SELECT * FROM \"Vehicles\" WHERE \"Id\" = {vehicleId} FOR UPDATE")
                .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
            var draft = await database.InspectionDrafts.FromSqlInterpolated(
                $"SELECT * FROM \"InspectionDrafts\" WHERE \"Id\" = {inspectionId} FOR UPDATE")
                .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
            if (vehicle is null || draft is null || draft.BranchId != actor.BranchId)
            {
                return new(InspectionAssignmentStatus.NotFound);
            }

            var existing = await database.InspectionRepairApprovals.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == request.OperationId, cancellationToken);
            if (existing is not null)
            {
                return existing.Matches(actorId, inspectionId, request)
                    ? new(InspectionAssignmentStatus.Succeeded, existing.Receipt()) : new(InspectionAssignmentStatus.Conflict);
            }

            var repair = await database.InspectionRepairRequests.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == request.RequestId, cancellationToken);
            if (!draft.IsSigned || draft.Revision != request.Revision || draft.AssignmentVersion != request.AssignmentVersion ||
                draft.RepairVersion != request.RepairVersion || vehicle.Status != VehicleStatus.PendingInspection ||
                repair is null || repair.InspectionId != inspectionId || repair.Revision != request.Revision ||
                repair.Version != request.RepairVersion || repair.AssignmentVersion != request.AssignmentVersion ||
                await database.InspectionRepairApprovals.AnyAsync(item => item.RequestId == request.RequestId, cancellationToken))
            {
                return new(InspectionAssignmentStatus.Conflict);
            }

            var now = await database.Database.SqlQuery<DateTimeOffset>($"SELECT clock_timestamp() AS \"Value\"")
                .SingleAsync(cancellationToken);
            var receipt = new InspectionRepairApprovalReceipt(request.OperationId, inspectionId, draft.BranchId, actorId,
                repair.Id, draft.AssignmentVersion, draft.Revision, draft.RepairVersion, now);
            // The immutable request owns approved scope/budget; later requests cannot rewrite this authority.
            database.InspectionRepairApprovals.Add(new(receipt));
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(InspectionAssignmentStatus.Succeeded, receipt);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return new(InspectionAssignmentStatus.Conflict);
        }
        finally
        {
            database.ChangeTracker.Clear();
        }
    }

    public async Task<InspectionRepairApprovalResult> ReadAsync(Guid actorId, Guid inspectionId, long revision,
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

        var latest = await database.InspectionRepairApprovals.AsNoTracking()
            .Where(item => item.InspectionId == inspectionId && item.Revision == revision)
            .OrderByDescending(item => item.RepairVersion).FirstOrDefaultAsync(cancellationToken);
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

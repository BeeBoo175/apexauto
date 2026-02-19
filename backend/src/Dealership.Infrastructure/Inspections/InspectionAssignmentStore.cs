using Dealership.Application.Accounts;
using Dealership.Application.Inspections;
using Dealership.Domain.Inspections;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Dealership.Infrastructure.Inspections;

public sealed class InspectionAssignmentStore(DealershipDbContext database) : IInspectionAssignmentStore
{
    public async Task<InspectionAssignmentResult> AssignAsync(Guid actorId, Guid vehicleId,
        InspectionAssignmentRequest request, CancellationToken cancellationToken)
    {
        if (actorId == Guid.Empty || vehicleId == Guid.Empty || request.OperationId == Guid.Empty ||
            request.TechnicianId == Guid.Empty || request.ExpectedVersion is < 0 or long.MaxValue)
        {
            return new(InspectionAssignmentStatus.Invalid);
        }

        try
        {
            request = request with { Reason = InspectionAssignment.NormalizeReason(request.Reason) };
        }
        catch (ArgumentException)
        {
            return new(InspectionAssignmentStatus.Invalid);
        }

        var claim = request.ExpectedVersion == 0;
        if (claim && (request.TechnicianId != actorId || request.Reason is not null) || !claim && request.Reason is null)
        {
            return new(InspectionAssignmentStatus.Invalid);
        }

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await database.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({"inspection-operation:" + request.OperationId}, 0))", cancellationToken);
            var users = await database.Users.FromSqlInterpolated($"""
                SELECT * FROM "AspNetUsers" WHERE "Id" IN ({actorId}, {request.TechnicianId}) ORDER BY "Id" FOR SHARE
                """).AsNoTracking().ToArrayAsync(cancellationToken);
            var actor = users.SingleOrDefault(item => item.Id == actorId);
            var role = claim ? AccountRoles.WorkshopTechnician : AccountRoles.BranchManager;
            if (actor is not { IsActive: true, BranchId: not null } || actor.Role != role)
            {
                return new(InspectionAssignmentStatus.Forbidden);
            }

            var activeBranch = await database.Branches.FromSqlInterpolated(
                $"SELECT * FROM \"Branches\" WHERE \"Id\" = {actor.BranchId} FOR SHARE")
                .AnyAsync(item => item.IsActive, cancellationToken);
            if (!activeBranch)
            {
                return new(InspectionAssignmentStatus.Forbidden);
            }

            var vehicle = await database.Vehicles.FromSqlInterpolated(
                $"SELECT * FROM \"Vehicles\" WHERE \"Id\" = {vehicleId} FOR UPDATE")
                .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
            if (vehicle is null || vehicle.BranchId != actor.BranchId)
            {
                return new(InspectionAssignmentStatus.NotFound);
            }

            var existing = await database.InspectionAssignments.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == request.OperationId, cancellationToken);
            if (existing is not null)
            {
                var sameVehicle = await database.InspectionDrafts.AnyAsync(
                    item => item.Id == existing.InspectionId && item.VehicleId == vehicleId, cancellationToken);
                var matches = sameVehicle && existing.ActorId == actorId && existing.TechnicianId == request.TechnicianId &&
                    existing.ExpectedVersion == request.ExpectedVersion && existing.Reason == request.Reason;
                return matches ? Success(existing, vehicleId) : new(InspectionAssignmentStatus.Conflict);
            }

            var technician = users.SingleOrDefault(item => item.Id == request.TechnicianId);
            if (technician is not { IsActive: true } || technician.Role != AccountRoles.WorkshopTechnician ||
                technician.BranchId != vehicle.BranchId || vehicle.Status != VehicleStatus.PendingInspection)
            {
                return new(InspectionAssignmentStatus.Conflict);
            }

            var draft = await database.InspectionDrafts.SingleOrDefaultAsync(item => item.VehicleId == vehicleId, cancellationToken);
            if (claim && draft is not null || !claim && (draft is null || draft.AssignmentVersion != request.ExpectedVersion ||
                draft.TechnicianId == request.TechnicianId || draft.IsSigned))
            {
                return new(InspectionAssignmentStatus.Conflict);
            }

            var now = await database.Database.SqlQuery<DateTimeOffset>($"SELECT clock_timestamp() AS \"Value\"")
                .SingleAsync(cancellationToken);
            Guid? previous = draft?.TechnicianId;
            if (draft is null)
            {
                draft = new InspectionDraft(request.OperationId, vehicle, request.TechnicianId, now);
                database.InspectionDrafts.Add(draft);
            }
            else
            {
                draft.Reassign(request.TechnicianId, request.ExpectedVersion);
            }

            // The deferred receipt guard keeps both writes atomic while the draft precedes its receipt.
            await database.SaveChangesAsync(cancellationToken);
            var assignment = new InspectionAssignment(request.OperationId, draft, actorId, previous,
                request.ExpectedVersion, request.Reason, now);
            database.InspectionAssignments.Add(assignment);
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Success(assignment, vehicleId);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return new(InspectionAssignmentStatus.Conflict);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new(InspectionAssignmentStatus.Conflict);
        }
        finally
        {
            database.ChangeTracker.Clear();
        }
    }

    public async Task<InspectionDraftResult> ReadAsync(Guid actorId, Guid inspectionId, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var actor = await database.Users.FromSqlInterpolated(
            $"SELECT * FROM \"AspNetUsers\" WHERE \"Id\" = {actorId} FOR SHARE")
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (actor is not { IsActive: true } ||
            actor.Role is not (AccountRoles.WorkshopTechnician or AccountRoles.BranchManager or AccountRoles.GeneralManager))
        {
            return new(InspectionAssignmentStatus.Forbidden);
        }

        if (actor.Role != AccountRoles.GeneralManager && (actor.BranchId is null ||
            !await database.Branches.FromSqlInterpolated($"SELECT * FROM \"Branches\" WHERE \"Id\" = {actor.BranchId} FOR SHARE")
                .AnyAsync(item => item.IsActive, cancellationToken)))
        {
            return new(InspectionAssignmentStatus.Forbidden);
        }

        var query = database.InspectionDrafts.FromSqlInterpolated(
            $"SELECT * FROM \"InspectionDrafts\" WHERE \"Id\" = {inspectionId} FOR SHARE").AsNoTracking();
        if (actor.Role != AccountRoles.GeneralManager)
        {
            query = query.Where(item => item.BranchId == actor.BranchId);
        }
        if (actor.Role == AccountRoles.WorkshopTechnician)
        {
            query = query.Where(item => item.TechnicianId == actorId);
        }

        var draft = await query.Select(item => new InspectionDraftSnapshot(item.Id, item.VehicleId, item.BranchId,
            item.TechnicianId, item.AssignmentVersion, item.CreatedAt, null)
        {
            Revision = item.Revision,
            IsSigned = item.IsSigned,
            ReopenedAfterSaveVersion = item.ReopenedAfterSaveVersion
        }).SingleOrDefaultAsync(cancellationToken);
        if (draft is not null)
        {
            var latest = await database.InspectionDraftSaves.AsNoTracking().Where(save => save.InspectionId == inspectionId)
                .OrderByDescending(save => save.Version).FirstOrDefaultAsync(cancellationToken);
            draft = draft with { LatestSave = latest?.Receipt() };
        }
        return draft is null ? new(InspectionAssignmentStatus.NotFound) : new(InspectionAssignmentStatus.Succeeded, draft);
    }

    private static InspectionAssignmentResult Success(InspectionAssignment assignment, Guid vehicleId)
    {
        return new(InspectionAssignmentStatus.Succeeded, new InspectionAssignmentReceipt(assignment.Id,
            assignment.InspectionId, vehicleId, assignment.BranchId, assignment.ActorId, assignment.TechnicianId,
            assignment.PreviousTechnicianId, assignment.ExpectedVersion, assignment.Version, assignment.Reason, assignment.RecordedAt));
    }
}

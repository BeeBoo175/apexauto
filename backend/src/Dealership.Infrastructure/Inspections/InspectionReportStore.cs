using Dealership.Application.Accounts;
using Dealership.Application.Inspections;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Identity;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Dealership.Infrastructure.Inspections;

public sealed class InspectionReportStore(DealershipDbContext database) : IInspectionReportStore
{
    public async Task<InspectionReportResult> SignAsync(Guid actorId, Guid inspectionId, InspectionSignRequest request,
        CancellationToken cancellationToken)
    {
        if (actorId == Guid.Empty || inspectionId == Guid.Empty || request.OperationId == Guid.Empty ||
            request.AssignmentVersion < 1 || request.SaveVersion < 1 || request.Revision < 1)
        {
            return new(InspectionAssignmentStatus.Invalid);
        }

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await database.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({"inspection-sign:" + request.OperationId}, 0))", cancellationToken);
            var actor = await LockActorAsync(actorId, cancellationToken);
            if (actor is not { Role: AccountRoles.WorkshopTechnician })
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
            if (vehicle is null || draft is null || draft.BranchId != actor.BranchId || draft.TechnicianId != actorId)
            {
                return new(InspectionAssignmentStatus.NotFound);
            }

            var existing = await database.InspectionSignedRevisions.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == request.OperationId, cancellationToken);
            if (existing is not null)
            {
                return existing.Matches(actorId, inspectionId, request)
                    ? new(InspectionAssignmentStatus.Succeeded, existing.Report()) : new(InspectionAssignmentStatus.Conflict);
            }

            var save = await database.InspectionDraftSaves.AsNoTracking().Where(item => item.InspectionId == inspectionId)
                .OrderByDescending(item => item.Version).FirstOrDefaultAsync(cancellationToken);
            if (draft.IsSigned || draft.Revision != request.Revision || draft.AssignmentVersion != request.AssignmentVersion ||
                vehicle.Status != VehicleStatus.PendingInspection || save is null || save.Version != request.SaveVersion ||
                save.ActorId != actorId || save.AssignmentVersion != request.AssignmentVersion || save.ExaminedPointCodesJson is null)
            {
                return new(InspectionAssignmentStatus.Conflict);
            }

            var photos = await database.InspectionDraftPhotos.Where(link => link.SaveId == save.Id)
                .Join(database.InspectionPhotos, link => link.PhotoId, photo => photo.Id, (_, photo) => photo)
                .AsNoTracking().ToArrayAsync(cancellationToken);
            var now = await database.Database.SqlQuery<DateTimeOffset>($"SELECT clock_timestamp() AS \"Value\"")
                .SingleAsync(cancellationToken);
            var report = InspectionReport.Sign(request.OperationId, draft.Revision, draft, vehicle, actor.UserName!, save.Receipt(), photos, now);
            database.InspectionSignedRevisions.Add(new(report));
            // The SQL insert seals the draft row atomically; older repeatable-read writers must conflict.
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(InspectionAssignmentStatus.Succeeded, report);
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

    public async Task<InspectionReportResult> ReadAsync(Guid actorId, Guid inspectionId, long revision,
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

        var drafts = database.InspectionDrafts.AsNoTracking();
        if (actor.Role != AccountRoles.GeneralManager)
        {
            drafts = drafts.Where(draft => draft.BranchId == actor.BranchId);
        }
        if (actor.Role == AccountRoles.WorkshopTechnician)
        {
            drafts = drafts.Where(draft => draft.TechnicianId == actorId);
        }

        var signed = await database.InspectionSignedRevisions.AsNoTracking()
            .Where(item => item.InspectionId == inspectionId && item.Revision == revision)
            .Join(drafts, item => item.InspectionId, draft => draft.Id, (item, _) => item)
            .SingleOrDefaultAsync(cancellationToken);
        return signed is null ? new(InspectionAssignmentStatus.NotFound)
            : new(InspectionAssignmentStatus.Succeeded, signed.Report());
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

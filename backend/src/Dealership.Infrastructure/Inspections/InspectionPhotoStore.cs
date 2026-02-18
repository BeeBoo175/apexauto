using Dealership.Application.Accounts;
using Dealership.Application.Inspections;
using Dealership.Domain.Inspections;
using Dealership.Domain.Photos;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Inspections;

public sealed class InspectionPhotoStore(DealershipDbContext database, IInspectionAssignmentStore assignments)
    : IInspectionPhotoStore
{
    public async Task<InspectionPhotoStatus> CheckUploadAsync(Guid actorId, Guid inspectionId, long assignmentVersion,
        CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        return (await LockUploadAsync(actorId, inspectionId, assignmentVersion, cancellationToken)).Status;
    }

    public async Task<InspectionPhotoPreparation> PrepareAsync(Guid actorId, Guid inspectionId, Guid id,
        long assignmentVersion, string pointCode, PhotoContent content, TimeSpan lifetime, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var access = await LockUploadAsync(actorId, inspectionId, assignmentVersion, cancellationToken);
            if (access.Status != InspectionPhotoStatus.Available)
            {
                return new(access.Status);
            }

            await database.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({"inspection-photo:" + id}, 0))", cancellationToken);
            var existing = await database.InspectionPhotos.AsNoTracking()
                .SingleOrDefaultAsync(photo => photo.Id == id, cancellationToken);
            var now = await DatabaseTimeAsync(cancellationToken);
            if (existing is not null)
            {
                return existing.Matches(inspectionId, actorId, assignmentVersion, pointCode, content)
                    ? Classify(existing, now) : new(InspectionPhotoStatus.Conflict);
            }

            var photo = new InspectionPhoto(id, inspectionId, access.Draft!.BranchId, actorId, assignmentVersion,
                pointCode, content, now, lifetime);
            database.InspectionPhotos.Add(photo);
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(InspectionPhotoStatus.Pending, photo);
        }
        finally
        {
            database.ChangeTracker.Clear();
        }
    }

    public async Task<InspectionPhotoPreparation> ConfirmAsync(Guid actorId, Guid inspectionId, Guid id,
        CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var access = await LockUploadAsync(actorId, inspectionId, null, cancellationToken);
            if (access.Status != InspectionPhotoStatus.Available)
            {
                return new(access.Status);
            }

            var photo = await database.InspectionPhotos.FromSqlInterpolated(
                $"SELECT * FROM \"InspectionPhotos\" WHERE \"Id\" = {id} AND \"InspectionId\" = {inspectionId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);
            if (photo is null)
            {
                return new(InspectionPhotoStatus.NotFound);
            }

            if (photo.ActorId != actorId || photo.AssignmentVersion != access.Draft!.AssignmentVersion)
            {
                return new(InspectionPhotoStatus.Conflict);
            }

            var now = await DatabaseTimeAsync(cancellationToken);
            var result = Classify(photo, now);
            if (result.Status != InspectionPhotoStatus.Pending)
            {
                return result;
            }

            photo.ConfirmAvailable(now);
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(InspectionPhotoStatus.Available, photo);
        }
        finally
        {
            database.ChangeTracker.Clear();
        }
    }

    public async Task<InspectionPhotoPreparation> FindAsync(Guid actorId, Guid inspectionId, Guid id,
        CancellationToken cancellationToken)
    {
        var access = await assignments.ReadAsync(actorId, inspectionId, cancellationToken);
        if (access.Status != InspectionAssignmentStatus.Succeeded)
        {
            return new(access.Status == InspectionAssignmentStatus.Forbidden
                ? InspectionPhotoStatus.Forbidden : InspectionPhotoStatus.NotFound);
        }

        var photo = await database.InspectionPhotos.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id && item.InspectionId == inspectionId, cancellationToken);
        return photo is null ? new(InspectionPhotoStatus.NotFound)
            : Classify(photo, await DatabaseTimeAsync(cancellationToken));
    }

    private static InspectionPhotoPreparation Classify(InspectionPhoto photo, DateTimeOffset now)
    {
        if (now >= photo.ExpiresAt)
        {
            return new(InspectionPhotoStatus.Gone, photo);
        }

        return new(photo.AvailableAt is null ? InspectionPhotoStatus.Pending : InspectionPhotoStatus.Available, photo);
    }

    private async Task<(InspectionPhotoStatus Status, InspectionDraft? Draft)> LockUploadAsync(Guid actorId,
        Guid inspectionId, long? assignmentVersion, CancellationToken cancellationToken)
    {
        var actor = await database.Users.FromSqlInterpolated(
            $"SELECT * FROM \"AspNetUsers\" WHERE \"Id\" = {actorId} FOR SHARE")
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (actor is not { IsActive: true, BranchId: not null, Role: AccountRoles.WorkshopTechnician } ||
            !await database.Branches.FromSqlInterpolated($"SELECT * FROM \"Branches\" WHERE \"Id\" = {actor.BranchId} FOR SHARE")
                .AnyAsync(branch => branch.IsActive, cancellationToken))
        {
            return (InspectionPhotoStatus.Forbidden, null);
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
            return (InspectionPhotoStatus.NotFound, null);
        }

        if (vehicle.Status != VehicleStatus.PendingInspection || assignmentVersion is < 1 ||
            assignmentVersion.HasValue && draft.AssignmentVersion != assignmentVersion)
        {
            return (InspectionPhotoStatus.Conflict, null);
        }

        return (InspectionPhotoStatus.Available, draft);
    }

    private Task<DateTimeOffset> DatabaseTimeAsync(CancellationToken cancellationToken)
    {
        return database.Database.SqlQuery<DateTimeOffset>($"SELECT clock_timestamp() AS \"Value\"")
            .SingleAsync(cancellationToken);
    }
}

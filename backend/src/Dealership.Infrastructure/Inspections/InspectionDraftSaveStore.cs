using Dealership.Application.Accounts;
using Dealership.Application.Inspections;
using Dealership.Domain.Inspections;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Dealership.Infrastructure.Inspections;

public sealed class InspectionDraftSaveStore(DealershipDbContext database) : IInspectionDraftSaveStore
{
    public async Task<InspectionDraftSaveResult> SaveAsync(Guid actorId, Guid inspectionId,
        InspectionDraftSaveRequest request, CancellationToken cancellationToken)
    {
        if (actorId == Guid.Empty || inspectionId == Guid.Empty || request.OperationId == Guid.Empty ||
            request.AssignmentVersion < 1 || request.ExpectedVersion is < 0 or long.MaxValue ||
            request.Entries is null || request.Entries.Count > InspectionCatalog.Points.Count || request.PhotoIds is null ||
            request.PhotoIds.Count > InspectionPhotoLimits.MaximumPerSave || request.PhotoIds.Contains(Guid.Empty) ||
            request.PhotoIds.Distinct().Count() != request.PhotoIds.Count)
        {
            return new(InspectionAssignmentStatus.Invalid);
        }

        InspectionDraftEntry[] entries;
        InspectionAssessment assessment;
        IReadOnlyList<string>? examinedPointCodes = null;
        try
        {
            if (request.Entries.Any(entry => entry is null))
            {
                return new(InspectionAssignmentStatus.Invalid);
            }

            entries = [.. request.Entries.Select(entry => new InspectionDraftEntry(entry.PointCode, entry.Kind,
                entry.Severity, entry.EstimatedRepairCost, entry.Notes)).OrderBy(entry => entry.PointCode, StringComparer.Ordinal)];
            assessment = InspectionGrading.Calculate(entries);
            if (request.ExaminedPointCodes is not null)
            {
                examinedPointCodes = InspectionChecklist.Attest(entries, request.ExaminedPointCodes);
            }
        }
        catch (ArgumentException)
        {
            return new(InspectionAssignmentStatus.Invalid);
        }

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await database.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({"inspection-save:" + request.OperationId}, 0))", cancellationToken);
            var actor = await database.Users.FromSqlInterpolated(
                $"SELECT * FROM \"AspNetUsers\" WHERE \"Id\" = {actorId} FOR SHARE")
                .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
            if (actor is not { IsActive: true, BranchId: not null, Role: AccountRoles.WorkshopTechnician } ||
                !await database.Branches.FromSqlInterpolated($"SELECT * FROM \"Branches\" WHERE \"Id\" = {actor.BranchId} FOR SHARE")
                    .AnyAsync(branch => branch.IsActive, cancellationToken))
            {
                return new(InspectionAssignmentStatus.Forbidden);
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
            if (vehicle is null || draft is null || draft.BranchId != actor.BranchId || draft.TechnicianId != actorId)
            {
                return new(InspectionAssignmentStatus.NotFound);
            }

            var existing = await database.InspectionDraftSaves.AsNoTracking()
                .SingleOrDefaultAsync(save => save.Id == request.OperationId, cancellationToken);
            if (existing is not null)
            {
                return existing.Matches(actorId, inspectionId, request, entries)
                    ? new(InspectionAssignmentStatus.Succeeded, existing.Receipt()) : new(InspectionAssignmentStatus.Conflict);
            }

            var version = await database.InspectionDraftSaves.Where(save => save.InspectionId == inspectionId)
                .Select(save => (long?)save.Version).MaxAsync(cancellationToken) ?? 0;
            if (draft.AssignmentVersion != request.AssignmentVersion || version != request.ExpectedVersion ||
                vehicle.Status != VehicleStatus.PendingInspection)
            {
                return new(InspectionAssignmentStatus.Conflict);
            }

            var photos = await database.InspectionPhotos.FromSqlInterpolated($"""
                SELECT * FROM "InspectionPhotos" WHERE "Id" = ANY({request.PhotoIds.ToArray()}) ORDER BY "Id" FOR UPDATE
                """).AsNoTracking().ToArrayAsync(cancellationToken);
            var now = await database.Database.SqlQuery<DateTimeOffset>($"SELECT clock_timestamp() AS \"Value\"")
                .SingleAsync(cancellationToken);
            var findingCodes = entries.Where(entry => entry.Kind == InspectionEntryKind.Finding)
                .Select(entry => entry.PointCode).ToHashSet(StringComparer.Ordinal);
            if (photos.Length != request.PhotoIds.Count || photos.Any(photo => photo.InspectionId != inspectionId ||
                photo.AvailableAt is null || !findingCodes.Contains(photo.PointCode)) ||
                photos.GroupBy(photo => photo.PointCode).Any(group => group.Count() > InspectionPhotoLimits.MaximumPerFinding))
            {
                return new(InspectionAssignmentStatus.Invalid);
            }
            var photoIds = photos.Select(photo => photo.Id).ToArray();
            var retained = await database.InspectionDraftPhotos.Where(link => photoIds.Contains(link.PhotoId))
                .Select(link => link.PhotoId).Distinct().ToArrayAsync(cancellationToken);
            if (photos.Any(photo => photo.ExpiresAt <= now && !retained.Contains(photo.Id)) ||
                await database.InspectionPhotoRemovals.AnyAsync(removal => photoIds.Contains(removal.PhotoId), cancellationToken))
            {
                return new(InspectionAssignmentStatus.Conflict);
            }

            var receipt = new InspectionDraftSaveReceipt(request.OperationId, inspectionId, actorId,
                request.AssignmentVersion, request.ExpectedVersion, request.ExpectedVersion + 1,
                InspectionCatalog.Version, entries,
                new(assessment.EstimatedRepairCost, assessment.Grade, assessment.IsRejected), now)
            {
                PhotoIds = request.PhotoIds.Order().ToArray(),
                ExaminedPointCodes = examinedPointCodes
            };
            var saved = new InspectionDraftSave(draft.BranchId, receipt);
            database.InspectionDraftSaves.Add(saved);
            await database.SaveChangesAsync(cancellationToken);
            database.InspectionDraftPhotos.AddRange(photos.Select(photo => new InspectionDraftPhoto
            {
                SaveId = saved.Id,
                InspectionId = inspectionId,
                PhotoId = photo.Id,
                PointCode = photo.PointCode
            }));
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(InspectionAssignmentStatus.Succeeded, receipt);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return new(InspectionAssignmentStatus.Conflict);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        { ConstraintName: "CK_InspectionDraftPhotos_Available" })
        {
            return new(InspectionAssignmentStatus.Conflict);
        }
        finally
        {
            database.ChangeTracker.Clear();
        }
    }
}

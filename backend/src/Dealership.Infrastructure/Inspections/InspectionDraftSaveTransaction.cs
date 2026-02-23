using Dealership.Application.Inspections;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Dealership.Infrastructure.Inspections;

internal sealed class InspectionDraftSaveTransaction(DealershipDbContext database, IDbContextTransaction transaction,
    InspectionDraftSaveContext context) : IInspectionDraftSaveTransaction
{
    public InspectionDraftSaveContext Context => context;

    public async Task<InspectionSavePhotoSet> LockPhotosAsync(IReadOnlyList<Guid> photoIds, CancellationToken cancellationToken)
    {
        var ids = photoIds.ToArray();
        var photos = await database.InspectionPhotos.FromSqlInterpolated($"""
            SELECT * FROM "InspectionPhotos" WHERE "Id" = ANY({ids}) ORDER BY "Id" FOR UPDATE
            """).AsNoTracking().ToArrayAsync(cancellationToken);
        var now = await database.Database.SqlQuery<DateTimeOffset>($"SELECT clock_timestamp() AS \"Value\"")
            .SingleAsync(cancellationToken);
        var retained = await database.InspectionDraftPhotos.Where(link => ids.Contains(link.PhotoId))
            .Select(link => link.PhotoId).Distinct().ToArrayAsync(cancellationToken);
        var removals = await database.InspectionPhotoRemovals.Where(removal => ids.Contains(removal.PhotoId))
            .Select(removal => removal.PhotoId).ToArrayAsync(cancellationToken);
        var candidates = photos.Select(photo => new InspectionSavePhoto(photo.Id, photo.InspectionId, photo.PointCode,
            photo.ExpiresAt, photo.AvailableAt, retained.Contains(photo.Id), removals.Contains(photo.Id))).ToArray();
        return new(candidates, now);
    }

    public async Task<InspectionAssignmentStatus> CommitAsync(InspectionDraftSaveReceipt receipt,
        IReadOnlyList<InspectionSavePhoto> photos, CancellationToken cancellationToken)
    {
        var saved = new InspectionDraftSave(context.Draft!.BranchId, receipt);
        database.InspectionDraftSaves.Add(saved);

        try
        {
            await database.SaveChangesAsync(cancellationToken);
            database.InspectionDraftPhotos.AddRange(photos.Select(photo => new InspectionDraftPhoto
            {
                SaveId = saved.Id,
                InspectionId = receipt.InspectionId,
                PhotoId = photo.Id,
                PointCode = photo.PointCode
            }));
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return InspectionAssignmentStatus.Succeeded;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        { SqlState: PostgresErrorCodes.UniqueViolation } or PostgresException { ConstraintName: "CK_InspectionDraftPhotos_Available" })
        {
            return InspectionAssignmentStatus.Conflict;
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await transaction.DisposeAsync();
        }
        finally
        {
            database.ChangeTracker.Clear();
        }
    }
}

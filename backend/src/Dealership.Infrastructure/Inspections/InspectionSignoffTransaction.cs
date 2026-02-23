using Dealership.Application.Inspections;
using Dealership.Domain.Inspections;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Dealership.Infrastructure.Inspections;

internal sealed class InspectionSignoffTransaction(DealershipDbContext database, IDbContextTransaction transaction,
    InspectionSignoffContext context) : IInspectionSignoffTransaction
{
    public InspectionSignoffContext Context => context;

    public async Task<InspectionSignoffEvidence> ReadEvidenceAsync(CancellationToken cancellationToken)
    {
        var inspectionId = context.Draft!.Id;
        var save = await database.InspectionDraftSaves.AsNoTracking().Where(item => item.InspectionId == inspectionId)
            .OrderByDescending(item => item.Version).FirstOrDefaultAsync(cancellationToken);
        InspectionPhoto[] photos = [];
        if (save is not null)
        {
            photos = await database.InspectionDraftPhotos.Where(link => link.SaveId == save.Id)
                .Join(database.InspectionPhotos, link => link.PhotoId, photo => photo.Id, (_, photo) => photo)
                .AsNoTracking().ToArrayAsync(cancellationToken);
        }

        var now = await database.Database.SqlQuery<DateTimeOffset>($"SELECT clock_timestamp() AS \"Value\"")
            .SingleAsync(cancellationToken);
        return new(save?.Receipt(), photos, now);
    }

    public async Task<InspectionAssignmentStatus> CommitAsync(InspectionReport report, CancellationToken cancellationToken)
    {
        database.InspectionSignedRevisions.Add(new(report));
        try
        {
            // The SQL insert seals the draft atomically; older repeatable-read writers must conflict.
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return InspectionAssignmentStatus.Succeeded;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        { SqlState: PostgresErrorCodes.UniqueViolation })
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

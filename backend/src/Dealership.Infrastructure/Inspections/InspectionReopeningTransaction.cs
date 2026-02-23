using Dealership.Application.Inspections;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Dealership.Infrastructure.Inspections;

internal sealed class InspectionReopeningTransaction(DealershipDbContext database, IDbContextTransaction transaction,
    InspectionReopeningContext context) : IInspectionReopeningTransaction
{
    public InspectionReopeningContext Context => context;

    public async Task<InspectionReopeningEvidence> ReadEvidenceAsync(CancellationToken cancellationToken)
    {
        var draft = context.Draft!;
        var signedId = await database.InspectionSignedRevisions.Where(item =>
            item.InspectionId == draft.Id && item.Revision == draft.Revision)
            .Select(item => item.Id).SingleAsync(cancellationToken);
        var latestVersion = await database.InspectionDraftSaves.Where(item => item.InspectionId == draft.Id)
            .MaxAsync(item => item.Version, cancellationToken);
        var now = await database.Database.SqlQuery<DateTimeOffset>($"SELECT clock_timestamp() AS \"Value\"")
            .SingleAsync(cancellationToken);
        return new(signedId, latestVersion, now);
    }

    public async Task<InspectionAssignmentStatus> CommitAsync(InspectionReopenReceipt receipt, CancellationToken cancellationToken)
    {
        database.InspectionReopenings.Add(new(receipt));
        try
        {
            // SQL advances the header and captures the last save; old attestation cannot cross revisions.
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

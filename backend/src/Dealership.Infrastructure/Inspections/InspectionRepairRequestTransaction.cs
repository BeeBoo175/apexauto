using Dealership.Application.Inspections;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Dealership.Infrastructure.Inspections;

internal sealed class InspectionRepairRequestTransaction(DealershipDbContext database, IDbContextTransaction transaction,
    InspectionRepairRequestContext context) : IInspectionRepairRequestTransaction
{
    public InspectionRepairRequestContext Context => context;

    public async Task<InspectionReport> ReadReportAsync(CancellationToken cancellationToken)
    {
        var draft = context.Draft!;
        var signed = await database.InspectionSignedRevisions.AsNoTracking()
            .SingleAsync(item => item.InspectionId == draft.Id && item.Revision == draft.Revision, cancellationToken);
        return signed.Report();
    }

    public async Task<DateTimeOffset> ReadDatabaseTimeAsync(CancellationToken cancellationToken)
    {
        return await database.Database.SqlQuery<DateTimeOffset>($"SELECT clock_timestamp() AS \"Value\"")
            .SingleAsync(cancellationToken);
    }

    public async Task<InspectionAssignmentStatus> CommitAsync(InspectionRepairReceipt receipt, CancellationToken cancellationToken)
    {
        database.InspectionRepairRequests.Add(new(receipt));
        try
        {
            // SQL advances the actual draft; older approval/completion snapshots must serialize.
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

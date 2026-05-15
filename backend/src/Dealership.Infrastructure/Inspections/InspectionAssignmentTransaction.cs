using Dealership.Application.Inspections;
using Dealership.Domain.Inspections;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Dealership.Infrastructure.Inspections;

internal sealed class InspectionAssignmentTransaction(DealershipDbContext database, IDbContextTransaction transaction,
    InspectionAssignmentContext context) : IInspectionAssignmentTransaction
{
    public InspectionAssignmentContext Context => context;

    public async Task<DateTimeOffset> ReadDatabaseTimeAsync(CancellationToken cancellationToken)
    {
        return await database.DatabaseNowAsync(cancellationToken);
    }

    public async Task<InspectionAssignmentStatus> CommitAsync(InspectionDraft draft, InspectionAssignment assignment,
        CancellationToken cancellationToken)
    {
        if (database.Entry(draft).State == EntityState.Detached)
        {
            database.InspectionDrafts.Add(draft);
        }

        try
        {
            // The deferred receipt guard keeps both writes atomic while the draft precedes its receipt.
            await database.SaveChangesAsync(cancellationToken);
            database.InspectionAssignments.Add(assignment);
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return InspectionAssignmentStatus.Succeeded;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return InspectionAssignmentStatus.Conflict;
        }
        catch (DbUpdateConcurrencyException)
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

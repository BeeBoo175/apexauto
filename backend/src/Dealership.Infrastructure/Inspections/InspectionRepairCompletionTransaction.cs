using System.Text.Json;
using Dealership.Application.Inspections;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Dealership.Infrastructure.Inspections;

internal sealed class InspectionRepairCompletionTransaction(DealershipDbContext database, IDbContextTransaction transaction,
    InspectionRepairCompletionContext context) : IInspectionRepairCompletionTransaction
{
    public InspectionRepairCompletionContext Context => context;

    public async Task<InspectionRepairCompletionEvidence> ReadEvidenceAsync(Guid requestId, CancellationToken cancellationToken)
    {
        var repair = await database.InspectionRepairRequests.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == requestId, cancellationToken);
        var alreadyCompleted = await database.InspectionRepairCompletions
            .AnyAsync(item => item.RequestId == requestId, cancellationToken);
        return new(repair?.Receipt(), alreadyCompleted);
    }

    public async Task<InspectionRepairApprovalReceipt?> ReadLatestApprovalAsync(InspectionRepairApprovalLookup lookup,
        CancellationToken cancellationToken)
    {
        var itemsJson = JsonSerializer.Serialize(lookup.ExactItems, JsonSerializerOptions.Web);
        var approval = await (
            from granted in database.InspectionRepairApprovals.AsNoTracking()
            join scope in database.InspectionRepairRequests.AsNoTracking() on granted.RequestId equals scope.Id
            where scope.InspectionId == lookup.InspectionId && scope.Revision == lookup.Revision &&
                scope.ItemsJson == itemsJson && scope.Budget >= lookup.MinimumBudget
            orderby granted.ApprovedAt descending, granted.Id descending
            select granted).FirstOrDefaultAsync(cancellationToken);
        return approval?.Receipt();
    }

    public async Task<DateTimeOffset> ReadDatabaseTimeAsync(CancellationToken cancellationToken)
    {
        return await database.Database.SqlQuery<DateTimeOffset>($"SELECT clock_timestamp() AS \"Value\"")
            .SingleAsync(cancellationToken);
    }

    public async Task<InspectionAssignmentStatus> CommitAsync(InspectionRepairCompletionReceipt receipt, CancellationToken cancellationToken)
    {
        database.InspectionRepairCompletions.Add(new(receipt));
        try
        {
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

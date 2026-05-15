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
        var approval = await InspectionRepairAuthority.CoveredRequests(database)
            .Where(item => item.Request.InspectionId == lookup.InspectionId && item.Request.Revision == lookup.Revision &&
                item.Request.ItemsJson == itemsJson && item.Request.Budget == lookup.MinimumBudget)
            .OrderByDescending(item => item.Approval.ApprovedAt).ThenByDescending(item => item.Approval.Id)
            .Select(item => item.Approval).FirstOrDefaultAsync(cancellationToken);
        return approval?.Receipt();
    }

    public async Task<DateTimeOffset> ReadDatabaseTimeAsync(CancellationToken cancellationToken)
    {
        return await database.DatabaseNowAsync(cancellationToken);
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

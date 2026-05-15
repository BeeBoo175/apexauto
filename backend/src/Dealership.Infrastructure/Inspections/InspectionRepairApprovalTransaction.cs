using Dealership.Application.Inspections;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Dealership.Infrastructure.Inspections;

internal sealed class InspectionRepairApprovalTransaction(DealershipDbContext database, IDbContextTransaction transaction,
    InspectionRepairApprovalContext context) : IInspectionRepairApprovalTransaction
{
    public InspectionRepairApprovalContext Context => context;

    public async Task<InspectionRepairApprovalEvidence> ReadEvidenceAsync(Guid requestId, CancellationToken cancellationToken)
    {
        var repair = await database.InspectionRepairRequests.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == requestId, cancellationToken);
        var alreadyApproved = await database.InspectionRepairApprovals
            .AnyAsync(item => item.RequestId == requestId, cancellationToken);
        return new(repair?.Receipt(), alreadyApproved);
    }

    public async Task<DateTimeOffset> ReadDatabaseTimeAsync(CancellationToken cancellationToken)
    {
        return await database.DatabaseNowAsync(cancellationToken);
    }

    public async Task<InspectionAssignmentStatus> CommitAsync(InspectionRepairApprovalReceipt receipt, CancellationToken cancellationToken)
    {
        database.InspectionRepairApprovals.Add(new(receipt));
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

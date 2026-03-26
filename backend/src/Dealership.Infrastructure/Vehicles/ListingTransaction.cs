using Dealership.Application.Vehicles;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Dealership.Infrastructure.Vehicles;

internal sealed class ListingTransaction(DealershipDbContext database, IDbContextTransaction transaction,
    ListingContext context) : IListingTransaction
{
    public ListingContext Context => context;

    public Task<ListingStatus> SaveAsync(ListingRevision revision, CancellationToken cancellationToken)
    {
        return CommitAsync(revision, cancellationToken);
    }

    public Task<ListingStatus> PublishAsync(ListingPublication publication, CancellationToken cancellationToken)
    {
        return CommitAsync(publication, cancellationToken);
    }

    public Task<ListingStatus> WithdrawAsync(ListingWithdrawal withdrawal, CancellationToken cancellationToken)
    {
        return CommitAsync(withdrawal, cancellationToken);
    }

    private async Task<ListingStatus> CommitAsync<TEntity>(TEntity operation, CancellationToken cancellationToken)
        where TEntity : class
    {
        database.Add(operation);
        try
        {
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ListingStatus.Succeeded;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return ListingStatus.Conflict;
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

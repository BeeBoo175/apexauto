using Dealership.Application.Vehicles;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Persistence;
using Dealership.Infrastructure.Mail;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Dealership.Infrastructure.Vehicles;

internal sealed class ListingTransaction(DealershipDbContext database, IDbContextTransaction transaction,
    MailDeliverySignal mail, ListingContext context) : IListingTransaction
{
    public ListingContext Context => context;

    public Task CompleteAsync(CancellationToken cancellationToken)
    {
        return transaction.CommitAsync(cancellationToken);
    }

    public Task<ListingStatus> SaveAsync(ListingRevision revision, IReadOnlyList<Guid> photoIds, CancellationToken cancellationToken)
    {
        database.ListingRevisionPhotos.AddRange(photoIds.Select((id, index) =>
            new ListingRevisionPhoto(revision.Id, id, revision.VehicleId, revision.BranchId, index)));
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
        await transaction.CreateSavepointAsync("listing_operation", cancellationToken);
        database.Add(operation);
        try
        {
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            if (operation is ListingWithdrawal)
            {
                mail.Request();
            }

            return ListingStatus.Succeeded;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // A rejected listing write must retain refunds reconciled before its savepoint.
            await transaction.RollbackToSavepointAsync("listing_operation", cancellationToken);
            database.ChangeTracker.Clear();
            await transaction.CommitAsync(cancellationToken);
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

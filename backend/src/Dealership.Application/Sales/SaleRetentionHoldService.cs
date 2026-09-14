namespace Dealership.Application.Sales;

public sealed class SaleRetentionHoldService(ISaleRetentionHoldStore store)
{
    public Task<SaleRetentionHoldResult> SetAsync(Guid actorId, Guid saleId, SaleRetentionHoldRequest request,
        CancellationToken cancellationToken)
    {
        if (actorId == Guid.Empty || saleId == Guid.Empty)
        {
            return Task.FromResult(new SaleRetentionHoldResult(SaleRetentionHoldStatus.Invalid));
        }

        // The future-date and reason rules need the database clock, so the store applies them under the lock.
        return store.SetAsync(actorId, saleId, request, cancellationToken);
    }
}

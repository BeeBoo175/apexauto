namespace Dealership.Application.Sales;

public sealed class SaleRetentionHoldService(ISaleRetentionHoldStore store)
{
    public Task<SaleRetentionHoldResult> SetByNumberAsync(Guid actorId, SaleRetentionHoldByNumberRequest request,
        CancellationToken cancellationToken)
    {
        var number = request.ReservationNumber?.Trim();
        if (actorId == Guid.Empty || string.IsNullOrEmpty(number) || number.Length > 40)
        {
            return Task.FromResult(new SaleRetentionHoldResult(SaleRetentionHoldStatus.Invalid));
        }

        // The future-date and reason rules need the database clock, so the store applies them under the lock.
        return store.SetByNumberAsync(actorId, number, new(request.Until, request.Reason), cancellationToken);
    }

    public Task<SaleRetentionHoldListResult> ListAsync(Guid actorId, CancellationToken cancellationToken) =>
        actorId == Guid.Empty
            ? Task.FromResult(new SaleRetentionHoldListResult(SaleRetentionHoldStatus.Forbidden))
            : store.ListAsync(actorId, cancellationToken);
}

namespace Dealership.Application.Sales;

// An Administrator's preservation hold keeps a sale's personal details past the retention deadline until the date
// passes. A request without a date releases the hold.
public sealed record SaleRetentionHoldRequest(DateTimeOffset? Until, string? Reason);

public sealed record SaleRetentionHoldView(Guid SaleId, DateTimeOffset? Until, string? Reason, Guid? SetById);

public enum SaleRetentionHoldStatus
{
    Succeeded,
    Invalid,
    Forbidden,
    NotFound
}

public sealed record SaleRetentionHoldResult(SaleRetentionHoldStatus Status, SaleRetentionHoldView? Hold = null);

public interface ISaleRetentionHoldStore
{
    Task<SaleRetentionHoldResult> SetAsync(Guid actorId, Guid saleId, SaleRetentionHoldRequest request,
        CancellationToken cancellationToken);
}

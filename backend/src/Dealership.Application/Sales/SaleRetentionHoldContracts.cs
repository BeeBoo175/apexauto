namespace Dealership.Application.Sales;

// An Administrator's preservation hold keeps a sale's personal details past the retention deadline until the date
// passes. A request without a date releases the hold.
public sealed record SaleRetentionHoldRequest(DateTimeOffset? Until, string? Reason);

public sealed record SaleRetentionHoldView(Guid SaleId, DateTimeOffset? Until, string? Reason, Guid? SetById);

// The Administrator names a sale by its reservation number, which staff and customers already see.
public sealed record SaleRetentionHoldByNumberRequest(string? ReservationNumber, DateTimeOffset? Until, string? Reason);

public sealed record SaleRetentionHoldRow(Guid SaleId, string ReservationNumber, DateTimeOffset Until, string Reason);

public sealed record SaleRetentionHoldListResult(SaleRetentionHoldStatus Status,
    IReadOnlyList<SaleRetentionHoldRow>? Holds = null);

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
    Task<SaleRetentionHoldResult> SetByNumberAsync(Guid actorId, string reservationNumber,
        SaleRetentionHoldRequest request, CancellationToken cancellationToken);

    Task<SaleRetentionHoldListResult> ListAsync(Guid actorId, CancellationToken cancellationToken);
}

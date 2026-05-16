using Dealership.Domain.Sales;

namespace Dealership.Application.Sales;

// Both settlement outputs retain the same reviewed and commercial evidence, independently of future live edits.
public sealed record SaleDocumentContents(Guid CustomerId, Guid BranchId, string BranchName, string BranchAddress,
    string ReservationNumber, SaleSettlementReview Review, SaleSnapshot Sale);

public enum SaleDocumentKind
{
    PurchaseOrder,
    PaymentReceipt,
    HandoverNote
}

public sealed record SaleIssuedDocument(Guid Id, string Kind, string Number, DateTimeOffset IssuedAt,
    SaleDocumentContents Contents);

public interface ISaleDocumentStore
{
    Task<SaleReadResult<SaleSettlementReview>> ReviewAsync(Guid actorId, Guid saleId,
        CancellationToken cancellationToken);

    Task<SaleReadResult<IReadOnlyList<SaleIssuedDocument>>> ReadAsync(Guid actorId, Guid saleId,
        CancellationToken cancellationToken);
}

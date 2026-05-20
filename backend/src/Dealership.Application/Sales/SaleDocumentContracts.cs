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

public sealed record SaleInspectionReport(Guid InspectionId, long Revision);

public interface ISaleDocumentRenderer
{
    byte[] Render(SaleIssuedDocument document);
}

public sealed record SaleIssuedDocument(Guid Id, string Kind, string Number, DateTimeOffset IssuedAt,
    SaleDocumentContents Contents)
{
    public Guid? ReplacesDocumentId { get; init; }

    public Guid? ReplacementDocumentId { get; init; }

    public SaleDocumentCorrectionReceipt? Correction { get; init; }
}

public interface ISaleDocumentStore
{
    Task<SaleReadResult<SaleInspectionReport>> InspectionReportAsync(Guid actorId, Guid saleId,
        CancellationToken cancellationToken);

    Task<SaleReadResult<SaleSettlementReview>> ReviewAsync(Guid actorId, Guid saleId,
        CancellationToken cancellationToken);

    Task<SaleReadResult<IReadOnlyList<SaleIssuedDocument>>> ReadAsync(Guid actorId, Guid saleId,
        CancellationToken cancellationToken);

    Task<SaleReadResult<SaleIssuedDocument>> ReadOneAsync(Guid actorId, Guid saleId, Guid documentId,
        CancellationToken cancellationToken);
}

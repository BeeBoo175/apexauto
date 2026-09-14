using Dealership.Domain.Sales;

namespace Dealership.Application.Sales;

public sealed record SaleDocumentCorrectionRequest(Guid OperationId, long ExpectedVersion, Guid DocumentId,
    SaleBuyerCorrection Correction);

public sealed record SaleDocumentCorrectionReceipt(Guid OperationId, Guid SaleId, Guid OriginalDocumentId,
    Guid ReplacementDocumentId, string ReplacementNumber, Guid ActorId, DateTimeOffset RecordedAt, string? Reason);

public sealed record SaleDocumentCorrectionRecord(SaleDocumentCorrectionRequest Request,
    SaleDocumentCorrectionReceipt Receipt);

public sealed record SaleDocumentCorrectionResult(SaleResultStatus Status,
    SaleDocumentCorrectionReceipt? Receipt = null);

public sealed record SaleCorrectionDocument(SaleIssuedDocument Document, bool IsVoided);

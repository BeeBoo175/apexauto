using Dealership.Application.Sales;

namespace Dealership.Infrastructure.Sales;

public sealed class SaleDocumentCorrection
{
    private SaleDocumentCorrection()
    {
    }

    public SaleDocumentCorrection(Guid saleId, Guid actorId, SaleDocumentCorrectionRequest request,
        DateTimeOffset recordedAt)
    {
        Id = request.OperationId;
        SalesOrderId = saleId;
        ActorId = actorId;
        OriginalDocumentId = request.DocumentId;
        ExpectedVersion = request.ExpectedVersion;
        BuyerName = request.Correction.BuyerName;
        BuyerAddress = request.Correction.BuyerAddress;
        Reason = request.Correction.Reason;
        RecordedAt = recordedAt;
    }

    public Guid Id { get; private set; }

    public Guid SalesOrderId { get; private set; }

    public Guid ActorId { get; private set; }

    public Guid OriginalDocumentId { get; private set; }

    public long ExpectedVersion { get; private set; }

    public string? BuyerName { get; private set; }

    public string? BuyerAddress { get; private set; }

    public string? Reason { get; private set; }

    // Set when the retention period removed the buyer spelling and the reason; the record itself remains.
    public DateTimeOffset? MinimizedAt { get; private set; }

    public DateTimeOffset RecordedAt { get; private set; }

    public SaleDocumentCorrectionRecord Record(SaleDocument replacement) => new(
        new(Id, ExpectedVersion, OriginalDocumentId,
            new(BuyerName ?? string.Empty, BuyerAddress ?? string.Empty, Reason ?? string.Empty)),
        new(Id, SalesOrderId, OriginalDocumentId, replacement.Id, replacement.Number, ActorId, RecordedAt, Reason));
}

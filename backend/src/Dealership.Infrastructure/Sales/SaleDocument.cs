using System.Text.Json;
using Dealership.Application.Sales;

namespace Dealership.Infrastructure.Sales;

public sealed class SaleDocument
{
    private SaleDocument()
    {
    }

    public SaleDocument(Guid saleId, Guid operationId, SaleDocumentKind kind, string number,
        DateTimeOffset issuedAt, SaleDocumentContents contents)
    {
        Id = Guid.NewGuid();
        SalesOrderId = saleId;
        SettlementOperationId = operationId;
        Kind = kind;
        Number = number;
        IssuedAt = issuedAt;
        ContentsJson = JsonSerializer.Serialize(contents);
    }

    public Guid Id { get; private set; }

    public Guid SalesOrderId { get; private set; }

    public Guid SettlementOperationId { get; private set; }

    public SaleDocumentKind Kind { get; private set; }

    public string Number { get; private set; } = string.Empty;

    public DateTimeOffset IssuedAt { get; private set; }

    public string ContentsJson { get; private set; } = string.Empty;

    public SaleDocumentContents Contents() => JsonSerializer.Deserialize<SaleDocumentContents>(ContentsJson)!;
}

namespace Dealership.Application.Sales;

public sealed class SaleDocumentService(ISaleDocumentStore documents, ISaleDocumentRenderer renderer)
{
    public async Task<SaleReadResult<byte[]>> ReadAsync(Guid actorId, Guid saleId, Guid documentId,
        CancellationToken cancellationToken)
    {
        var read = await documents.ReadOneAsync(actorId, saleId, documentId, cancellationToken);
        if (read.Status != SaleResultStatus.Succeeded)
        {
            return new(read.Status);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var content = renderer.Render(read.Value!);
        cancellationToken.ThrowIfCancellationRequested();
        return new(SaleResultStatus.Succeeded, content);
    }
}

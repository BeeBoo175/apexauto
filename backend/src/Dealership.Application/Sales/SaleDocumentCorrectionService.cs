using Dealership.Domain.Sales;

namespace Dealership.Application.Sales;

public sealed class SaleDocumentCorrectionService(ISaleCommandStore sales)
{
    public async Task<SaleDocumentCorrectionResult> ReissueAsync(Guid actorId, Guid saleId,
        SaleDocumentCorrectionRequest request, CancellationToken cancellationToken)
    {
        if (!SaleCommandSteps.Valid(actorId, saleId, request.OperationId, request.ExpectedVersion) ||
            request.DocumentId == Guid.Empty || request.Correction is null)
        {
            return new(SaleResultStatus.Invalid);
        }

        try
        {
            request = request with { Correction = request.Correction.Normalize() };
        }
        catch (ArgumentException)
        {
            return new(SaleResultStatus.Invalid);
        }

        await using var transaction = await sales.LockAsync(actorId, saleId, request.OperationId, cancellationToken);
        var (actor, sale, failure) = SaleCommandSteps.Scope(transaction.Context);
        if (failure is not null || actor is null || sale is null)
        {
            return new(failure ?? SaleResultStatus.NotFound);
        }

        // Corrections have their own evidence; the delivered sale and its version remain frozen.
        if (!SaleAccess.IsManager(actor, sale.BranchId))
        {
            return new(SaleResultStatus.Forbidden);
        }

        var existing = await transaction.ReadCorrectionAsync(request.OperationId, cancellationToken);
        if (existing is not null)
        {
            return existing.Request == request && existing.Receipt.ActorId == actorId && existing.Receipt.SaleId == saleId
                ? new(SaleResultStatus.Succeeded, existing.Receipt)
                : new(SaleResultStatus.Conflict);
        }

        // A correction would write new personal details for someone who has closed their account.
        if (transaction.Context.CustomerClosed)
        {
            return new(SaleResultStatus.CustomerClosed);
        }

        if (transaction.Context.Existing is not null || sale.Version != request.ExpectedVersion ||
            sale.Status is not (SalesOrderStatus.Settled or SalesOrderStatus.Delivered))
        {
            return new(SaleResultStatus.Conflict);
        }

        var original = await transaction.ReadCorrectionDocumentAsync(request.DocumentId, cancellationToken);
        if (original is null)
        {
            return new(SaleResultStatus.NotFound);
        }

        // Retention removed this document's buyer details; a correction would write new ones into evidence.
        if (original.Document.Contents.Review.BuyerDetailsRemoved)
        {
            return new(SaleResultStatus.DetailsRemoved);
        }

        if (original.IsVoided)
        {
            return new(SaleResultStatus.Conflict);
        }

        SaleDocumentContents contents;
        try
        {
            contents = original.Document.Contents with
            {
                Review = request.Correction.Apply(original.Document.Contents.Review)
            };
        }
        catch (ArgumentException)
        {
            return new(SaleResultStatus.Invalid);
        }

        return await transaction.CommitCorrectionAsync(actorId, request, contents, cancellationToken);
    }
}

using Dealership.Application.Sales;
using Dealership.Domain.Sales;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Sales;

internal static class SaleDocumentCorrections
{
    public static async Task<SaleDocumentCorrectionRecord?> ReadAsync(DealershipDbContext database, Guid operationId,
        CancellationToken cancellationToken)
    {
        var correction = await database.SaleDocumentCorrections.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == operationId, cancellationToken);
        if (correction is null)
        {
            return null;
        }

        var replacement = await database.SaleDocuments.AsNoTracking()
            .SingleAsync(item => item.CorrectionOperationId == operationId, cancellationToken);
        return correction.Record(replacement);
    }

    public static async Task<SaleCorrectionDocument?> ReadDocumentAsync(DealershipDbContext database, Guid saleId,
        Guid documentId, CancellationToken cancellationToken)
    {
        var document = await database.SaleDocuments.AsNoTracking().SingleOrDefaultAsync(item =>
            item.Id == documentId && item.SalesOrderId == saleId, cancellationToken);
        if (document is null)
        {
            return null;
        }

        var voided = await database.SaleDocumentCorrections.AnyAsync(item => item.OriginalDocumentId == documentId,
            cancellationToken);
        return new(new(document.Id, document.Kind.ToString(), document.Number, document.IssuedAt, document.Contents()), voided);
    }

    public static async Task<SaleDocumentCorrectionResult> AddAsync(DealershipDbContext database, Guid saleId,
        Guid actorId, SaleDocumentCorrectionRequest request, SaleDocumentContents contents, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var original = await database.SaleDocuments.AsNoTracking().SingleAsync(item =>
            item.Id == request.DocumentId && item.SalesOrderId == saleId, cancellationToken);
        var year = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(now, "Asia/Kuala_Lumpur").Year;
        var number = original.Kind switch
        {
            SaleDocumentKind.PurchaseOrder => await YearlyNumberAllocator.NextAsync<DealNumberCounter>(database, year, cancellationToken),
            SaleDocumentKind.PaymentReceipt => await YearlyNumberAllocator.NextAsync<ReceiptNumberCounter>(database, year, cancellationToken),
            SaleDocumentKind.HandoverNote => await YearlyNumberAllocator.NextAsync<HandoverNumberCounter>(database, year, cancellationToken),
            _ => throw new InvalidOperationException("Unsupported sales document kind.")
        };
        var correction = new SaleDocumentCorrection(saleId, actorId, request, now);
        var replacement = new SaleDocument(saleId, original.IssuingOperationId, original.Kind, number, now, contents,
            correction.Id);
        database.SaleDocumentCorrections.Add(correction);
        database.SaleDocuments.Add(replacement);
        await database.SaveChangesAsync(cancellationToken);
        return new(SaleResultStatus.Succeeded, correction.Record(replacement).Receipt);
    }
}

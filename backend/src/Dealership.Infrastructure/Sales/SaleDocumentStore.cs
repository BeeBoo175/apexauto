using Dealership.Application.Accounts;
using Dealership.Application.Sales;
using Dealership.Domain.Sales;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Sales;

public sealed class SaleDocumentStore(DealershipDbContext database) : ISaleDocumentStore
{
    public async Task<SaleReadResult<SaleInspectionReport>> InspectionReportAsync(Guid actorId, Guid saleId,
        CancellationToken cancellationToken)
    {
        var (status, actor, sale) = await SaleReadScope.ReadAsync(database, actorId, saleId, cancellationToken,
            allowCustomer: true);
        if (sale is null)
        {
            return new(status);
        }

        if (actor!.Role != AccountRoles.Customer)
        {
            return new(SaleResultStatus.Forbidden);
        }

        if (sale.Status != SalesOrderStatus.Delivered)
        {
            return new(SaleResultStatus.NotFound);
        }

        // Discovery selects the reserved publication's signed evidence, never today's inspection revision.
        var report = await (from reservation in database.Reservations
                            join publication in database.ListingPublications on reservation.PublicationId equals publication.Id
                            join signed in database.InspectionSignedRevisions on publication.SignedRevisionId equals signed.Id
                            where reservation.Id == saleId
                            select new SaleInspectionReport(signed.InspectionId, signed.Revision))
            .SingleOrDefaultAsync(cancellationToken);
        return report is null
            ? new(SaleResultStatus.NotFound)
            : new(SaleResultStatus.Succeeded, report);
    }

    public async Task<SaleReadResult<SaleSettlementReview>> ReviewAsync(Guid actorId, Guid saleId,
        CancellationToken cancellationToken)
    {
        var (status, actor, sale) = await SaleReadScope.ReadAsync(database, actorId, saleId, cancellationToken);
        if (sale is null)
        {
            return new(status);
        }

        if (!SaleAccess.MayKeyTerms(actor!, sale) && !SaleAccess.IsManager(actor!, sale.BranchId))
        {
            return new(SaleResultStatus.NotFound);
        }

        var profile = await database.CustomerProfiles.AsNoTracking()
            .SingleAsync(item => item.UserId == sale.CustomerId, cancellationToken);
        var vehicle = await database.Vehicles.AsNoTracking()
            .SingleAsync(item => item.Id == sale.VehicleId, cancellationToken);
        return new(SaleResultStatus.Succeeded, new SaleSettlementReview(profile.LegalName, profile.Nric,
            profile.ResidentialAddress, SaleReviewedVehicle.Of(vehicle)));
    }

    public async Task<SaleReadResult<IReadOnlyList<SaleIssuedDocument>>> ReadAsync(Guid actorId, Guid saleId,
        CancellationToken cancellationToken)
    {
        var (status, _, sale) = await SaleReadScope.ReadAsync(database, actorId, saleId, cancellationToken,
            allowCustomer: true);
        if (sale is null)
        {
            return new(status);
        }

        // One current leaf per kind; older bodies remain reachable through the immutable replacement links.
        var documents = await database.SaleDocuments.AsNoTracking().Where(item => item.SalesOrderId == saleId &&
                !database.SaleDocumentCorrections.Any(correction => correction.OriginalDocumentId == item.Id))
            .OrderBy(item => item.IssuedAt).ThenBy(item => item.Kind).ThenBy(item => item.Id)
            .Take(3)
            .ToListAsync(cancellationToken);
        var results = new List<SaleIssuedDocument>();
        foreach (var document in documents)
        {
            results.Add(await DescribeAsync(document, cancellationToken));
        }

        return new(SaleResultStatus.Succeeded, results);
    }

    public async Task<SaleReadResult<SaleIssuedDocument>> ReadOneAsync(Guid actorId, Guid saleId, Guid documentId,
        CancellationToken cancellationToken)
    {
        var (status, _, sale) = await SaleReadScope.ReadAsync(database, actorId, saleId, cancellationToken,
            allowCustomer: true);
        if (sale is null)
        {
            return new(status);
        }

        var document = await database.SaleDocuments.AsNoTracking().SingleOrDefaultAsync(item =>
            item.Id == documentId && item.SalesOrderId == saleId, cancellationToken);
        return document is null
            ? new(SaleResultStatus.NotFound)
            : new(SaleResultStatus.Succeeded, await DescribeAsync(document, cancellationToken));
    }

    private async Task<SaleIssuedDocument> DescribeAsync(SaleDocument document, CancellationToken cancellationToken)
    {
        var incoming = document.CorrectionOperationId is { } operationId
            ? await SaleDocumentCorrections.ReadAsync(database, operationId, cancellationToken)
            : null;
        var outgoingId = await database.SaleDocumentCorrections.AsNoTracking()
            .Where(item => item.OriginalDocumentId == document.Id).Select(item => (Guid?)item.Id)
            .SingleOrDefaultAsync(cancellationToken);
        var outgoing = outgoingId is { } id
            ? await SaleDocumentCorrections.ReadAsync(database, id, cancellationToken)
            : null;
        return new(document.Id, document.Kind.ToString(), document.Number, document.IssuedAt, document.Contents())
        {
            ReplacesDocumentId = incoming?.Receipt.OriginalDocumentId,
            ReplacementDocumentId = outgoing?.Receipt.ReplacementDocumentId,
            Correction = outgoing?.Receipt ?? incoming?.Receipt
        };
    }
}

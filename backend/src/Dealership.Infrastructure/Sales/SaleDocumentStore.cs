using Dealership.Application.Sales;
using Dealership.Domain.Sales;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Sales;

public sealed class SaleDocumentStore(DealershipDbContext database) : ISaleDocumentStore
{
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

        var documents = await database.SaleDocuments.AsNoTracking().Where(item => item.SalesOrderId == saleId)
            .OrderBy(item => item.IssuedAt).ThenBy(item => item.Kind).ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);
        return new(SaleResultStatus.Succeeded, documents.Select(item => new SaleIssuedDocument(item.Id,
            item.Kind.ToString(), item.Number, item.IssuedAt, item.Contents())).ToList());
    }
}

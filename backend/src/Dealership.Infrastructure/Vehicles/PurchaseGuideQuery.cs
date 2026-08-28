using Dealership.Application.Vehicles;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Vehicles;

public sealed class PurchaseGuideQuery(DealershipDbContext database) : IPurchaseGuideQuery
{
    public async Task<PurchaseGuide?> ReadAsync(CancellationToken cancellationToken)
    {
        var settings = await database.PurchaseSettings.AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (settings is null)
        {
            return null;
        }

        var warranties = await database.WarrantyPackages.AsNoTracking().Where(package => package.IsActive)
            .OrderBy(package => package.Price).ThenBy(package => package.Id)
            .Select(package => new PurchaseGuideWarranty(package.Id, package.Name, package.DurationMonths,
                package.MileageCapKm, package.Price, package.MinimumGrade.ToString()))
            .ToArrayAsync(cancellationToken);
        return new PurchaseGuide(settings.ProcessingFee, settings.SstRate, warranties);
    }
}
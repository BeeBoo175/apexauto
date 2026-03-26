using Dealership.Application.Vehicles;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Vehicles;

public sealed class PurchaseConfigurationStore(DealershipDbContext database) : IPurchaseConfigurationStore
{
    public async Task<PurchaseConfigurationResult> ProvisionAsync(PurchaseSettings settings, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        await LockAsync(cancellationToken);
        var existing = await database.PurchaseSettings.AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (existing is not null)
        {
            return new(false, Matches(existing, new(settings.ProcessingFee, settings.SstRate, settings.UpdatedById)));
        }

        database.PurchaseSettings.Add(settings);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(true, true);
    }

    public async Task<PurchaseConfigurationResult> ProvisionAsync(PromoCode promo, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        await LockAsync(cancellationToken);
        var existing = await database.PromoCodes.AsNoTracking()
            .Where(item => item.Id == promo.Id || item.Code == promo.Code).ToArrayAsync(cancellationToken);
        if (existing.Length > 0)
        {
            var matches = existing.Length == 1 && Matches(existing[0],
                new(promo.Id, promo.Code, promo.Discount, promo.ExpiresAtUtc, promo.IsActive));
            return new(false, matches);
        }

        database.PromoCodes.Add(promo);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(true, true);
    }

    internal static bool Matches(PurchaseSettings existing, PurchaseSettingsRequest requested) =>
        existing.ProcessingFee == requested.ProcessingFee && existing.SstRate == requested.SstRate &&
        existing.UpdatedById == requested.UpdatedById;

    internal static bool Matches(PromoCode existing, PromoProvisioningRequest requested) =>
        existing.Id == requested.Id && existing.Code == requested.Code.Trim() && existing.Discount == requested.Discount &&
        existing.ExpiresAtUtc == requested.ExpiresAtUtc && existing.IsActive == requested.IsActive;

    private async Task LockAsync(CancellationToken cancellationToken)
    {
        // Initial provisioning must serialize absent rows and conflicting promo ID/code pairs across processes.
        await database.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({"purchase-configuration"}, 0))", cancellationToken);
    }
}

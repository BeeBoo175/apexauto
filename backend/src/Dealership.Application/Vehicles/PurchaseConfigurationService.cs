using Dealership.Domain.Vehicles;

namespace Dealership.Application.Vehicles;

// Trusted initial provisioning has no HTTP writer; administrative updates belong to the later settings workflow.
public sealed class PurchaseConfigurationService(IPurchaseConfigurationStore store, TimeProvider clock)
{
    public Task<PurchaseConfigurationResult> ProvisionSettingsAsync(PurchaseSettingsRequest request,
        CancellationToken cancellationToken)
    {
        var settings = new PurchaseSettings(request.ProcessingFee, request.SstRate, request.UpdatedById, clock.GetUtcNow());
        return store.ProvisionAsync(settings, cancellationToken);
    }

    public Task<PurchaseConfigurationResult> ProvisionPromoAsync(PromoProvisioningRequest request,
        CancellationToken cancellationToken)
    {
        var promo = new PromoCode(request.Id, request.Code, request.Discount, request.ExpiresAtUtc, request.IsActive);
        return store.ProvisionAsync(promo, cancellationToken);
    }

    public Task<PurchaseConfigurationResult> ProvisionWarrantyAsync(WarrantyProvisioningRequest request,
        CancellationToken cancellationToken)
    {
        var package = new WarrantyPackage(request.Id, request.Name, request.DurationMonths, request.MileageCapKm,
            request.Price, request.MinimumGrade, request.IsActive);
        return store.ProvisionAsync(package, cancellationToken);
    }
}

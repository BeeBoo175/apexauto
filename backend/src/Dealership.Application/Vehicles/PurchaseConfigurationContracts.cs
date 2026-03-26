using Dealership.Domain.Vehicles;

namespace Dealership.Application.Vehicles;

public sealed record PurchaseSettingsRequest(decimal ProcessingFee, decimal SstRate, Guid UpdatedById);

public sealed record PromoProvisioningRequest(Guid Id, string Code, decimal Discount, DateTimeOffset ExpiresAtUtc,
    bool IsActive = true);

public sealed record PurchaseConfigurationResult(bool Created, bool MatchesRequestedState);

public interface IPurchaseConfigurationStore
{
    Task<PurchaseConfigurationResult> ProvisionAsync(PurchaseSettings settings, CancellationToken cancellationToken);

    Task<PurchaseConfigurationResult> ProvisionAsync(PromoCode promo, CancellationToken cancellationToken);
}

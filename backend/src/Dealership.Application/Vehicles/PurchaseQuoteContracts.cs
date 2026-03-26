using Dealership.Domain.Vehicles;

namespace Dealership.Application.Vehicles;

public sealed record PromoValidationRequest(Guid VehicleId, string? Code);

public enum PurchaseQuoteStatus
{
    Succeeded,
    Invalid,
    NotFound,
    PromoUnavailable,
    Unavailable
}

public sealed record PurchaseQuote(string? PromoCode, PurchasePrice Price);

public sealed record PurchaseQuoteResult(PurchaseQuoteStatus Status, PurchaseQuote? Quote = null);

public sealed record PurchaseListingPrice(decimal VehiclePrice, decimal RoadTax);

public sealed record PurchaseQuoteContext(PurchaseListingPrice? Listing, PurchaseSettings? Settings,
    PromoCode? Promo, DateTimeOffset Now);

public interface IPurchaseQuoteStore
{
    Task<PurchaseQuoteContext> ReadAsync(Guid vehicleId, string? code, CancellationToken cancellationToken);
}

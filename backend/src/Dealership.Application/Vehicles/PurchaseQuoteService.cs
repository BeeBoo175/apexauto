using Dealership.Domain.Vehicles;

namespace Dealership.Application.Vehicles;

public sealed class PurchaseQuoteService(IPurchaseQuoteStore store)
{
    public async Task<PurchaseQuoteResult> QuoteAsync(Guid vehicleId, string? code, CancellationToken cancellationToken)
    {
        if (vehicleId == Guid.Empty || code is not null && !PromoCode.ValidCode(code))
        {
            return new(PurchaseQuoteStatus.Invalid);
        }

        var context = await store.ReadAsync(vehicleId, code?.Trim(), cancellationToken);
        if (!context.IsAvailable)
        {
            return new(PurchaseQuoteStatus.Unavailable);
        }

        if (context.Listing is null)
        {
            return new(PurchaseQuoteStatus.NotFound);
        }

        if (context.Settings is null)
        {
            return new(PurchaseQuoteStatus.Unavailable);
        }

        if (code is not null && (context.Promo is null ||
            !context.Promo.CanApply(context.Listing.VehiclePrice, context.Now)))
        {
            return new(PurchaseQuoteStatus.PromoUnavailable);
        }

        var price = PurchasePricing.CalculatePrice(context.Listing.VehiclePrice, context.Settings.ProcessingFee,
            context.Listing.RoadTax, context.Settings.SstRate, context.Promo?.Discount ?? 0m);
        return new(PurchaseQuoteStatus.Succeeded, new(context.Promo?.Code, price) { PromoId = context.Promo?.Id });
    }
}

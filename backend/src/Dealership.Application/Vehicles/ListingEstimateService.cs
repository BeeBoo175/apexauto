using Dealership.Domain.Vehicles;

namespace Dealership.Application.Vehicles;

public sealed class ListingEstimateService(IListingEstimateStore store)
{
    public async Task<ListingEstimateResult> EstimateAsync(Guid actorId, Guid vehicleId,
        ListingEstimateRequest request, CancellationToken cancellationToken)
    {
        if (actorId == Guid.Empty || vehicleId == Guid.Empty || request.BranchId == Guid.Empty ||
            request.VehiclePrice is not { } vehiclePrice || request.RoadTax is not { } roadTax)
        {
            return new(ListingStatus.Invalid);
        }

        var context = await store.ReadAsync(actorId, vehicleId, request.BranchId, cancellationToken);
        if (context.Status != ListingStatus.Succeeded)
        {
            return new(context.Status);
        }

        if (context.Settings is not { } settings)
        {
            return new(ListingStatus.Unavailable);
        }

        try
        {
            var price = PurchasePricing.CalculatePrice(vehiclePrice, settings.ProcessingFee, roadTax, settings.SstRate);
            return new(ListingStatus.Succeeded, new(actorId, vehicleId, request.BranchId, price, settings.SstRate));
        }
        catch (ArgumentException)
        {
            return new(ListingStatus.Invalid);
        }
        catch (OverflowException)
        {
            return new(ListingStatus.Invalid);
        }
    }
}

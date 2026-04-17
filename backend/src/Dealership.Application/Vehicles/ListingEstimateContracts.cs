using System.Text.Json.Serialization;
using Dealership.Application.Common;
using Dealership.Domain.Vehicles;

namespace Dealership.Application.Vehicles;

public sealed record ListingEstimateRequest(Guid BranchId,
    [property: JsonConverter(typeof(MyrAmountJsonConverter))] decimal? VehiclePrice,
    [property: JsonConverter(typeof(MyrAmountJsonConverter))] decimal? RoadTax);

public sealed record ListingEstimate(Guid ActorId, Guid VehicleId, Guid BranchId, PurchasePrice Price, decimal SstRate);

public sealed record ListingEstimateResult(ListingStatus Status, ListingEstimate? Estimate = null);

public sealed record ListingEstimateContext(ListingStatus Status, PurchaseSettings? Settings = null);

public interface IListingEstimateStore
{
    Task<ListingEstimateContext> ReadAsync(Guid actorId, Guid vehicleId, Guid branchId,
        CancellationToken cancellationToken);
}

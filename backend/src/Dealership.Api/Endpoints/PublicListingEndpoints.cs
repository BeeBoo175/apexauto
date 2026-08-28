using Dealership.Api.Configuration;
using Dealership.Application.Vehicles;
using Dealership.Domain.Vehicles;

namespace Dealership.Api.Endpoints;

public static class PublicListingEndpoints
{
    public static void MapPublicListingEndpoints(this WebApplication app)
    {
        var routes = app.MapGroup("/api/vehicles").NoStore();
        routes.MapGet("", ReadAsync);
        routes.MapGet("/{vehicleId:guid}", DetailAsync);
    }

    private static async Task<IResult> ReadAsync(int? page, int? pageSize, string? make, string? model, int? minimumYear,
        int? maximumYear, decimal? minimumPrice, decimal? maximumPrice, VehicleTransmission? transmission,
        VehicleFuel? fuel, Guid? branchId, string? sort, string? direction, string? search, IPublicListingStore store,
        CancellationToken cancellationToken)
    {
        return await BoundedCommand.RunAsync(async deadline =>
        {
            var filter = new PublicListingFilter(page ?? 1, make, model, minimumYear, maximumYear,
                minimumPrice, maximumPrice, transmission, fuel, branchId, sort ?? "published", direction ?? "desc",
                pageSize ?? PublicListingFilter.PageSize, search);
            var result = await store.ReadAsync(filter, deadline);
            return result.Status switch
            {
                ListingStatus.Succeeded => Results.Ok(result.Catalogue),
                ListingStatus.Invalid => Invalid(),
                ListingStatus.Unavailable => Unavailable(),
                _ => Results.Problem(statusCode: 500)
            };
        }, Unavailable, cancellationToken);
    }

    private static async Task<IResult> DetailAsync(Guid vehicleId, IPublicListingStore store,
        CancellationToken cancellationToken)
    {
        return await BoundedCommand.RunAsync(async deadline =>
        {
            var result = await store.DetailAsync(vehicleId, deadline);
            return result.Status switch
            {
                ListingStatus.Succeeded => Results.Ok(result.Detail),
                ListingStatus.Invalid => Invalid(),
                ListingStatus.NotFound => Results.NotFound(),
                ListingStatus.Unavailable => Unavailable(),
                _ => Results.Problem(statusCode: 500)
            };
        }, Unavailable, cancellationToken);
    }

    private static IResult Invalid() => Results.Problem(statusCode: 400, title: "Provide valid catalogue filters and page.");

    private static IResult Unavailable() => Results.Problem(statusCode: 503, title: "The catalogue is temporarily unavailable.");
}

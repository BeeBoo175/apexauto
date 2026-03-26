using Dealership.Application.Vehicles;
using Dealership.Domain.Vehicles;

namespace Dealership.Api.Endpoints;

public static class PublicListingEndpoints
{
    public static void MapPublicListingEndpoints(this WebApplication app)
    {
        var routes = app.MapGroup("/api/vehicles");
        routes.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            return await next(context);
        });
        routes.MapGet("", ReadAsync);
        routes.MapGet("/{vehicleId:guid}", DetailAsync);
    }

    private static async Task<IResult> ReadAsync(int? page, string? make, string? model, int? minimumYear,
        int? maximumYear, decimal? minimumPrice, decimal? maximumPrice, VehicleTransmission? transmission,
        VehicleFuel? fuel, Guid? branchId, string? sort, string? direction, IPublicListingStore store,
        CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            var filter = new PublicListingFilter(page ?? 1, make, model, minimumYear, maximumYear,
                minimumPrice, maximumPrice, transmission, fuel, branchId, sort ?? "published", direction ?? "desc");
            var result = await store.ReadAsync(filter, deadline.Token);
            return result.Status == ListingStatus.Succeeded ? Results.Ok(result.Catalogue) : Invalid();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Unavailable();
        }
    }

    private static async Task<IResult> DetailAsync(Guid vehicleId, IPublicListingStore store,
        CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            var result = await store.DetailAsync(vehicleId, deadline.Token);
            return result.Status switch
            {
                ListingStatus.Succeeded => Results.Ok(result.Detail),
                ListingStatus.Invalid => Invalid(),
                ListingStatus.NotFound => Results.NotFound(),
                ListingStatus.Unavailable => Unavailable(),
                _ => Results.Problem(statusCode: 500)
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Unavailable();
        }
    }

    private static IResult Invalid() => Results.Problem(statusCode: 400, title: "Provide valid catalogue filters and page.");

    private static IResult Unavailable() => Results.Problem(statusCode: 503, title: "The catalogue is temporarily unavailable.");
}

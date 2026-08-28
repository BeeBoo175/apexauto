using Dealership.Api.Configuration;
using Dealership.Application.Vehicles;

namespace Dealership.Api.Endpoints;

public static class PurchaseGuideEndpoints
{
    public static void MapPurchaseGuideEndpoints(this WebApplication app)
    {
        app.MapGroup("/api/purchase-guide").NoStore().MapGet("", ReadAsync);
    }

    private static async Task<IResult> ReadAsync(IPurchaseGuideQuery guide, CancellationToken cancellationToken)
    {
        return await BoundedRead.RunAsync(async deadline =>
        {
            var result = await guide.ReadAsync(deadline);
            return result is null ? Results.Problem(statusCode: 503, title: "Purchase terms are temporarily unavailable.")
                : Results.Ok(result);
        }, "Purchase terms are temporarily unavailable.", cancellationToken);
    }
}
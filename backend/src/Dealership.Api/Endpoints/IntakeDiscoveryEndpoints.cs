using Dealership.Application.Intake;

namespace Dealership.Api.Endpoints;

public static class IntakeDiscoveryEndpoints
{
    public static void MapIntakeDiscoveryEndpoints(this WebApplication app)
    {
        var routes = app.MapGroup("/api/branches");
        routes.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            return await next(context);
        });
        routes.MapGet("/{branchId:guid}/inspection-slots", GetSlotsAsync);
    }

    private static async Task<IResult> GetSlotsAsync(Guid branchId, string? date, IntakeDiscoveryService discovery,
        CancellationToken cancellationToken)
    {
        var result = await discovery.GetSlotsAsync(branchId, date, cancellationToken);
        return result.Status switch
        {
            SlotDiscoveryStatus.Succeeded => Results.Ok(result.Offer),
            SlotDiscoveryStatus.BranchUnavailable => Results.NotFound(),
            _ => Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["date"] = ["Choose a yyyy-MM-dd Malaysian date inside the current inspection horizon."]
            })
        };
    }
}

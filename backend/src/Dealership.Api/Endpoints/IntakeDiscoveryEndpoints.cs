using Dealership.Application.Intake;
using System.Globalization;

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
        routes.MapGet("", GetBranchesAsync);
        routes.MapGet("/{branchId:guid}/inspection-slots", GetSlotsAsync);
    }

    private static async Task<IResult> GetBranchesAsync(string? page, string? pageSize, IntakeDiscoveryService discovery,
        CancellationToken cancellationToken)
    {
        if (!int.TryParse(page ?? "1", NumberStyles.None, CultureInfo.InvariantCulture, out var selectedPage) ||
            !int.TryParse(pageSize ?? IntakeDiscoveryService.DefaultPageSize.ToString(CultureInfo.InvariantCulture),
                NumberStyles.None, CultureInfo.InvariantCulture, out var selectedSize))
        {
            return InvalidPagination();
        }

        var result = await discovery.GetBranchesAsync(selectedPage, selectedSize, cancellationToken);
        return result is null
            ? InvalidPagination()
            : Results.Ok(result);
    }

    private static IResult InvalidPagination()
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["pagination"] = ["Use a positive page and a page size between 1 and 50."]
        });
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

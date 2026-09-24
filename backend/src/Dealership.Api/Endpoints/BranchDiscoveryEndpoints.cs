using System.Globalization;
using Dealership.Application.Branches;
using Dealership.Application.Common;

namespace Dealership.Api.Endpoints;

public static class BranchDiscoveryEndpoints
{
    public static void MapBranchDiscoveryEndpoints(this WebApplication app)
    {
        var routes = app.MapGroup("/api/branches");
        routes.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            return await next(context);
        });
        routes.MapGet("", GetBranchesAsync);
    }

    private static async Task<IResult> GetBranchesAsync(string? page, string? pageSize, BranchDiscoveryService discovery,
        CancellationToken cancellationToken)
    {
        if (!int.TryParse(page ?? "1", NumberStyles.None, CultureInfo.InvariantCulture, out var selectedPage) ||
            !int.TryParse(pageSize ?? Pagination.DefaultPageSize.ToString(CultureInfo.InvariantCulture),
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
            ["pagination"] = ["Use a positive page and a page size of 10, 25 or 50."]
        });
    }
}

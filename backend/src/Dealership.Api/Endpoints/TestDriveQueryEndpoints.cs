using Dealership.Api.Configuration;
using Dealership.Application.Common;
using Dealership.Application.TestDrives;

namespace Dealership.Api.Endpoints;

public static class TestDriveQueryEndpoints
{
    public static void MapTestDriveQueryEndpoints(this WebApplication app)
    {
        var routes = app.MapGroup("/api");
        routes.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            return await next(context);
        });
        routes.MapGet("/vehicles/{vehicleId:guid}/test-drive-slots", SlotsAsync);
        routes.MapGet("/account/test-drives", AccountAsync).RequireAuthorization();
    }

    private static async Task<IResult> SlotsAsync(Guid vehicleId, string? date,
        TestDriveDiscoveryService discovery, CancellationToken cancellationToken)
    {
        return await BoundedCommand.RunAsync(async deadline =>
        {
            var result = await discovery.GetSlotsAsync(vehicleId, date, deadline);
            return result.Status switch
            {
                TestDriveReadStatus.Succeeded => Results.Ok(result.Offer),
                TestDriveReadStatus.NotFound => Results.NotFound(),
                _ => Results.Problem(statusCode: 400, title: "Choose a yyyy-MM-dd Malaysian date inside the current booking horizon.")
            };
        }, Unavailable, cancellationToken);
    }

    private static async Task<IResult> AccountAsync(int? page, string? from, string? to, string? search, HttpContext context,
        ITestDriveReadStore reads, CancellationToken cancellationToken)
    {
        var dates = ListDateRange.Parse(from, to);
        if (dates is null)
        {
            return Results.Problem(statusCode: 400, title: "Provide a paired Malaysian date range of at most 366 days.");
        }

        return await BoundedCommand.RunAsync(async deadline =>
        {
            var result = await reads.AccountAsync(HttpContextActor.ActorId(context), page ?? 1, dates,
                deadline, search?.Trim() ?? "");
            return result.Status switch
            {
                TestDriveReadStatus.Succeeded => Results.Ok(result.History),
                TestDriveReadStatus.Invalid => Results.Problem(statusCode: 400, title: "Provide a valid history page and search of at most 160 characters."),
                TestDriveReadStatus.Forbidden => Results.Forbid(),
                _ => Results.Problem(statusCode: 500)
            };
        }, Unavailable, cancellationToken);
    }

    private static IResult Unavailable() => Results.Problem(statusCode: 503, title: "Test-drive information is temporarily unavailable.");
}

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
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            var result = await discovery.GetSlotsAsync(vehicleId, date, deadline.Token);
            return result.Status switch
            {
                TestDriveReadStatus.Succeeded => Results.Ok(result.Offer),
                TestDriveReadStatus.NotFound => Results.NotFound(),
                _ => Results.Problem(statusCode: 400, title: "Choose a yyyy-MM-dd Malaysian date inside the current booking horizon.")
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Unavailable();
        }
    }

    private static async Task<IResult> AccountAsync(int? page, string? from, string? to, HttpContext context,
        ITestDriveReadStore reads, CancellationToken cancellationToken)
    {
        var dates = ListDateRange.Parse(from, to);
        if (dates is null)
        {
            return Results.Problem(statusCode: 400, title: "Provide a paired Malaysian date range of at most 366 days.");
        }

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            var result = await reads.AccountAsync(TestDriveCommandEndpoints.ActorId(context), page ?? 1, dates, deadline.Token);
            return result.Status switch
            {
                TestDriveReadStatus.Succeeded => Results.Ok(result.History),
                TestDriveReadStatus.Invalid => Results.Problem(statusCode: 400, title: "Provide a valid history page."),
                TestDriveReadStatus.Forbidden => Results.Forbid(),
                _ => Results.Problem(statusCode: 500)
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Unavailable();
        }
    }

    private static IResult Unavailable() => Results.Problem(statusCode: 503, title: "Test-drive information is temporarily unavailable.");
}

using System.Globalization;
using System.Text;
using Dealership.Application.Common;
using Dealership.Application.Intake;
using Dealership.Application.Performance;

namespace Dealership.Api.Endpoints;

public static class PerformanceQueryEndpoints
{
    public static void MapPerformanceQueryEndpoints(this WebApplication app)
    {
        var routes = app.MapGroup("/api/performance").RequireAuthorization();
        routes.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            return await next(context);
        });
        routes.MapGet("/headline", HeadlineAsync);
        routes.MapGet("/pipeline", PipelineAsync);
        routes.MapGet("/stock-flow", StockFlowAsync);
        routes.MapGet("/stage-waiting", StageWaitingAsync);
        routes.MapGet("/workload", WorkloadAsync);
        routes.MapGet("/roster", RosterAsync);
        routes.MapGet("/branches", BranchComparisonAsync);
        routes.MapGet("/team/sales", SalesTeamAsync);
        routes.MapGet("/team/workshop", WorkshopTeamAsync);
        routes.MapGet("/team/sales/export", ExportSalesTeamAsync);
        routes.MapGet("/team/workshop/export", ExportWorkshopTeamAsync);
    }

    private static async Task<IResult> HeadlineAsync(HttpContext context, IPerformanceReads reads,
        CancellationToken cancellationToken, string? from = null, string? to = null, Guid? branchId = null)
    {
        return await ReadAsync(from, to, branchId,
            (query, token) => reads.HeadlineAsync(HttpContextActor.ActorId(context), query, token), cancellationToken);
    }

    private static async Task<IResult> PipelineAsync(HttpContext context, IPerformanceReads reads,
        CancellationToken cancellationToken, string? from = null, string? to = null, Guid? branchId = null)
    {
        return await ReadAsync(from, to, branchId,
            (query, token) => reads.PipelineAsync(HttpContextActor.ActorId(context), query, token), cancellationToken);
    }

    private static async Task<IResult> StockFlowAsync(HttpContext context, IPerformanceReads reads,
        CancellationToken cancellationToken, string? from = null, string? to = null, Guid? branchId = null)
    {
        return await ReadAsync(from, to, branchId,
            (query, token) => reads.StockFlowAsync(HttpContextActor.ActorId(context), query, token), cancellationToken);
    }

    private static async Task<IResult> StageWaitingAsync(HttpContext context, IPerformanceReads reads,
        CancellationToken cancellationToken, string? from = null, string? to = null, Guid? branchId = null)
    {
        return await ReadAsync(from, to, branchId,
            (query, token) => reads.StageWaitingAsync(HttpContextActor.ActorId(context), query, token), cancellationToken);
    }

    private static async Task<IResult> WorkloadAsync(HttpContext context, IPerformanceReads reads,
        CancellationToken cancellationToken, string? from = null, string? to = null, Guid? branchId = null)
    {
        return await ReadAsync(from, to, branchId,
            (query, token) => reads.WorkloadAsync(HttpContextActor.ActorId(context), query, token), cancellationToken);
    }

    private static async Task<IResult> BranchComparisonAsync(HttpContext context, IPerformanceReads reads,
        CancellationToken cancellationToken, string? from = null, string? to = null)
    {
        return await ReadAsync(from, to, null,
            (query, token) => reads.BranchComparisonAsync(HttpContextActor.ActorId(context), query, token),
            cancellationToken);
    }

    private static async Task<IResult> RosterAsync(HttpContext context, IPerformanceReads reads,
        CancellationToken cancellationToken, string? from = null, string? to = null, Guid? branchId = null)
    {
        return await ReadAsync(from, to, branchId,
            (query, token) => reads.RosterAsync(HttpContextActor.ActorId(context), query, token), cancellationToken);
    }

    private static async Task<IResult> SalesTeamAsync(HttpContext context, IPerformanceReads reads,
        CancellationToken cancellationToken, string? from = null, string? to = null, Guid? branchId = null,
        int page = 1, int pageSize = Pagination.DefaultPageSize)
    {
        return await ReadTeamAsync(from, to, branchId, page, pageSize,
            (query, token) => reads.SalesTeamAsync(HttpContextActor.ActorId(context), query, token), cancellationToken);
    }

    private static async Task<IResult> WorkshopTeamAsync(HttpContext context, IPerformanceReads reads,
        CancellationToken cancellationToken, string? from = null, string? to = null, Guid? branchId = null,
        int page = 1, int pageSize = Pagination.DefaultPageSize)
    {
        return await ReadTeamAsync(from, to, branchId, page, pageSize,
            (query, token) => reads.WorkshopTeamAsync(HttpContextActor.ActorId(context), query, token),
            cancellationToken);
    }

    private static async Task<IResult> ExportSalesTeamAsync(HttpContext context, IPerformanceReads reads,
        CancellationToken cancellationToken, string? from = null, string? to = null, Guid? branchId = null)
    {
        return await ExportTeamAsync(from, to, branchId, "team-output-sales-and-service.csv",
            PerformanceTeamCsv.SalesHeader, PerformanceTeamCsv.Row,
            (query, token) => reads.SalesTeamAsync(HttpContextActor.ActorId(context), query, token), context,
            cancellationToken);
    }

    private static async Task<IResult> ExportWorkshopTeamAsync(HttpContext context, IPerformanceReads reads,
        CancellationToken cancellationToken, string? from = null, string? to = null, Guid? branchId = null)
    {
        return await ExportTeamAsync(from, to, branchId, "team-output-workshop.csv", PerformanceTeamCsv.WorkshopHeader,
            PerformanceTeamCsv.Row,
            (query, token) => reads.WorkshopTeamAsync(HttpContextActor.ActorId(context), query, token), context,
            cancellationToken);
    }

    // Exports every row of the tab with the same query and attribution as the page, in fixed alphabetical order.
    private static async Task<IResult> ExportTeamAsync<T>(string? from, string? to, Guid? branchId, string fileName,
        string header, Func<T, string> line,
        Func<PerformanceTeamQuery, CancellationToken, Task<PerformanceReadResult<PerformanceTeamPage<T>>>> read,
        HttpContext context, CancellationToken cancellationToken)
    {
        const int pageSize = 50;
        var period = PerformanceQuery.Parse(from, to, branchId);
        if (PerformanceTeamQuery.Parse(period, 1, pageSize) is not { } first)
        {
            return Results.Problem(statusCode: 400, title: "Provide a valid period of at most 366 days.");
        }

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            var csv = new StringBuilder(header);
            var count = 0;
            for (var page = 1; ; page++)
            {
                var result = await read(first with { Page = page }, deadline.Token);
                if (result.Status == PerformanceReadStatus.Forbidden)
                {
                    return Results.Forbid();
                }

                if (result.Value!.Total > StaffExportLimits.ImmediateRows)
                {
                    return Results.Problem(statusCode: 422, title: "This export is too large. Narrow the branch or period.");
                }

                foreach (var item in result.Value.Items)
                {
                    csv.Append(line(item));
                    count++;
                }

                if (count >= result.Value.Total || result.Value.Items.Count == 0)
                {
                    break;
                }
            }

            context.Response.Headers["X-Export-Row-Count"] = count.ToString(CultureInfo.InvariantCulture);
            return Results.File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv; charset=utf-8", fileName);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Results.Problem(statusCode: 503, title: "Export did not complete. Retry deliberately.");
        }
    }

    private static async Task<IResult> ReadTeamAsync<T>(string? from, string? to, Guid? branchId, int page, int pageSize,
        Func<PerformanceTeamQuery, CancellationToken, Task<PerformanceReadResult<PerformanceTeamPage<T>>>> read,
        CancellationToken cancellationToken)
    {
        var query = PerformanceTeamQuery.Parse(PerformanceQuery.Parse(from, to, branchId), page, pageSize);
        if (query is null)
        {
            return Results.Problem(statusCode: 400, title: "Provide a valid period of at most 366 days and page.");
        }

        return await ReadAsync(from, to, branchId, (_, token) => read(query, token), cancellationToken);
    }

    private static async Task<IResult> ReadAsync<T>(string? from, string? to, Guid? branchId,
        Func<PerformanceQuery, CancellationToken, Task<PerformanceReadResult<T>>> read, CancellationToken cancellationToken)
        where T : class
    {
        var query = PerformanceQuery.Parse(from, to, branchId);
        if (query is null)
        {
            return Results.Problem(statusCode: 400, title: "Provide a valid period of at most 366 days.");
        }

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            var result = await read(query, deadline.Token);
            return result.Status switch
            {
                PerformanceReadStatus.Succeeded => Results.Ok(result.Value),
                PerformanceReadStatus.Forbidden => Results.Forbid(),
                _ => Results.Problem(statusCode: 400, title: "Provide a valid performance request.")
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Results.Problem(statusCode: 503, title: "Performance figures are temporarily unavailable.");
        }
    }
}

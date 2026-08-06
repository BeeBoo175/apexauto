using Dealership.Api.Configuration;
using Dealership.Application.Common;
using Dealership.Application.Performance;

namespace Dealership.Api.Endpoints;

public static class PerformanceQueryEndpoints
{
    public static void MapPerformanceQueryEndpoints(this WebApplication app)
    {
        var routes = app.MapGroup("/api/performance").RequireAuthorization().NoStore();
        routes.MapGet("/headline", HeadlineAsync);
        routes.MapGet("/pipeline", PipelineAsync);
        routes.MapGet("/stock-flow", StockFlowAsync);
        routes.MapGet("/stage-waiting", StageWaitingAsync);
        routes.MapGet("/workload", WorkloadAsync);
        routes.MapGet("/branches", BranchComparisonAsync);
        routes.MapGet("/people/{personId:guid}/milestones", MilestonesAsync);
        routes.MapGet("/team/sales", SalesTeamAsync);
        routes.MapGet("/team/workshop", WorkshopTeamAsync);
    }

    private const string INVALID_PERIOD = "Provide a valid period of at most 366 days.";

    private const string INVALID_PERIOD_AND_PAGE = "Provide a valid period of at most 366 days and page.";

    private static async Task<IResult> HeadlineAsync(HttpContext context, IPerformanceSummaryQuery reads,
        CancellationToken cancellationToken, string? from = null, string? to = null, Guid? branchId = null)
    {
        return await ReadAsync(PerformanceQuery.Parse(from, to, branchId), INVALID_PERIOD,
            (query, token) => reads.HeadlineAsync(HttpContextActor.ActorId(context), query, token), cancellationToken);
    }

    private static async Task<IResult> PipelineAsync(HttpContext context, IPerformanceSummaryQuery reads,
        CancellationToken cancellationToken, string? from = null, string? to = null, Guid? branchId = null)
    {
        return await ReadAsync(PerformanceQuery.Parse(from, to, branchId), INVALID_PERIOD,
            (query, token) => reads.PipelineAsync(HttpContextActor.ActorId(context), query, token), cancellationToken);
    }

    private static async Task<IResult> StockFlowAsync(HttpContext context, IPerformanceStockReads reads,
        CancellationToken cancellationToken, string? from = null, string? to = null, Guid? branchId = null)
    {
        return await ReadAsync(PerformanceQuery.Parse(from, to, branchId), INVALID_PERIOD,
            (query, token) => reads.StockFlowAsync(HttpContextActor.ActorId(context), query, token), cancellationToken);
    }

    private static async Task<IResult> StageWaitingAsync(HttpContext context, IPerformanceStockReads reads,
        CancellationToken cancellationToken, string? from = null, string? to = null, Guid? branchId = null)
    {
        return await ReadAsync(PerformanceQuery.Parse(from, to, branchId), INVALID_PERIOD,
            (query, token) => reads.StageWaitingAsync(HttpContextActor.ActorId(context), query, token),
            cancellationToken);
    }

    private static async Task<IResult> WorkloadAsync(HttpContext context, IPerformanceWorkloadReads reads,
        CancellationToken cancellationToken, string? from = null, string? to = null, Guid? branchId = null,
        string? view = null, string? search = null, int page = 1, int pageSize = Pagination.DefaultPageSize)
    {
        var query = PerformanceWorkloadQuery.Parse(PerformanceQuery.Parse(from, to, branchId), view, search, page,
            pageSize);
        return await ReadAsync(query, "Provide a valid period of at most 366 days, team, search and page.",
            (valid, token) => reads.WorkloadAsync(HttpContextActor.ActorId(context), valid, token), cancellationToken);
    }

    private static async Task<IResult> BranchComparisonAsync(HttpContext context, IPerformanceSummaryQuery reads,
        CancellationToken cancellationToken, string? from = null, string? to = null)
    {
        return await ReadAsync(PerformanceQuery.Parse(from, to, null), INVALID_PERIOD,
            (query, token) => reads.BranchComparisonAsync(HttpContextActor.ActorId(context), query, token),
            cancellationToken);
    }

    private static async Task<IResult> MilestonesAsync(Guid personId, HttpContext context,
        IPerformanceMilestoneQuery reads, CancellationToken cancellationToken, string? from = null, string? to = null,
        Guid? branchId = null, int page = 1, int pageSize = Pagination.DefaultPageSize)
    {
        var query = PerformanceMilestoneQuery.Parse(PerformanceQuery.Parse(from, to, branchId), personId, page, pageSize);
        return await ReadAsync(query, INVALID_PERIOD_AND_PAGE,
            (valid, token) => reads.MilestonesAsync(HttpContextActor.ActorId(context), valid, token),
            cancellationToken);
    }

    private static async Task<IResult> SalesTeamAsync(HttpContext context, IPerformanceSalesTeamReads reads,
        CancellationToken cancellationToken, string? from = null, string? to = null, Guid? branchId = null,
        int page = 1, int pageSize = Pagination.DefaultPageSize, string? search = null, string? name = null,
        string? status = null, string? sort = null, string? direction = null, string? min = null)
    {
        var filter = PerformanceTeamFilter.Parse(search, name, status, sort, direction, min);
        var query = ParseTeam(from, to, branchId, page, pageSize, filter);
        return await ReadAsync(query, INVALID_PERIOD_AND_PAGE,
            (valid, token) => reads.SalesTeamAsync(HttpContextActor.ActorId(context), valid, token), cancellationToken);
    }

    private static async Task<IResult> WorkshopTeamAsync(HttpContext context, IPerformanceWorkshopTeamReads reads,
        CancellationToken cancellationToken, string? from = null, string? to = null, Guid? branchId = null,
        int page = 1, int pageSize = Pagination.DefaultPageSize, string? search = null, string? name = null,
        string? status = null, string? sort = null, string? direction = null, string? min = null)
    {
        var filter = PerformanceTeamFilter.Parse(search, name, status, sort, direction, min);
        var query = ParseTeam(from, to, branchId, page, pageSize, filter);
        return await ReadAsync(query, INVALID_PERIOD_AND_PAGE,
            (valid, token) => reads.WorkshopTeamAsync(HttpContextActor.ActorId(context), valid, token),
            cancellationToken);
    }

    private static PerformanceTeamQuery? ParseTeam(string? from, string? to, Guid? branchId, int page, int pageSize,
        PerformanceTeamFilter? filter)
    {
        return filter is null ? null :
            PerformanceTeamQuery.Parse(PerformanceQuery.Parse(from, to, branchId), page, pageSize, filter);
    }

    private static async Task<IResult> ReadAsync<TQuery, T>(TQuery? query, string invalidTitle,
        Func<TQuery, CancellationToken, Task<PerformanceReadResult<T>>> read, CancellationToken cancellationToken)
        where TQuery : class
        where T : class
    {
        if (query is null)
        {
            return Results.Problem(statusCode: 400, title: invalidTitle);
        }

        return await BoundedRead.RunAsync(async token =>
        {
            var result = await read(query, token);
            return result.Status switch
            {
                PerformanceReadStatus.Succeeded => Results.Ok(result.Value),
                PerformanceReadStatus.Forbidden => Results.Forbid(),
                PerformanceReadStatus.NotFound => Results.NotFound(),
                _ => Results.Problem(statusCode: 400, title: "Provide a valid performance request.")
            };
        }, "Performance figures are temporarily unavailable.", cancellationToken);
    }
}

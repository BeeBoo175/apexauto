using Dealership.Application.Common;
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
        routes.MapGet("/people/{personId:guid}/milestones", MilestonesAsync);
        routes.MapGet("/team/sales", SalesTeamAsync);
        routes.MapGet("/team/workshop", WorkshopTeamAsync);
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
        CancellationToken cancellationToken, string? from = null, string? to = null, Guid? branchId = null,
        string? team = null, string? search = null, int page = 1, int pageSize = Pagination.DefaultPageSize)
    {
        var query = PerformanceWorkloadQuery.Parse(PerformanceQuery.Parse(from, to, branchId), team, search, page,
            pageSize);
        if (query is null)
        {
            return Results.Problem(statusCode: 400,
                title: "Provide a valid period of at most 366 days, team, search and page.");
        }

        return await ReadAsync(from, to, branchId, (_, token) => reads.WorkloadAsync(
            HttpContextActor.ActorId(context), query, token), cancellationToken);
    }

    private static async Task<IResult> BranchComparisonAsync(HttpContext context, IPerformanceReads reads,
        CancellationToken cancellationToken, string? from = null, string? to = null)
    {
        return await ReadAsync(from, to, null,
            (query, token) => reads.BranchComparisonAsync(HttpContextActor.ActorId(context), query, token),
            cancellationToken);
    }

    private static async Task<IResult> MilestonesAsync(Guid personId, HttpContext context, IPerformanceReads reads,
        CancellationToken cancellationToken, string? from = null, string? to = null, Guid? branchId = null,
        int page = 1, int pageSize = Pagination.DefaultPageSize)
    {
        var query = PerformanceMilestoneQuery.Parse(PerformanceQuery.Parse(from, to, branchId), personId, page, pageSize);
        if (query is null)
        {
            return Results.Problem(statusCode: 400, title: "Provide a valid period of at most 366 days and page.");
        }

        return await ReadAsync(from, to, branchId, (_, token) => reads.MilestonesAsync(
            HttpContextActor.ActorId(context), query, token), cancellationToken);
    }

    private static async Task<IResult> RosterAsync(HttpContext context, IPerformanceReads reads,
        CancellationToken cancellationToken, string? from = null, string? to = null, Guid? branchId = null)
    {
        return await ReadAsync(from, to, branchId,
            (query, token) => reads.RosterAsync(HttpContextActor.ActorId(context), query, token), cancellationToken);
    }

    private static async Task<IResult> SalesTeamAsync(HttpContext context, IPerformanceReads reads,
        CancellationToken cancellationToken, string? from = null, string? to = null, Guid? branchId = null,
        int page = 1, int pageSize = Pagination.DefaultPageSize, string? search = null, string? name = null,
        string? status = null, string? sort = null, string? direction = null, string? min = null)
    {
        var filter = PerformanceTeamFilter.Parse(search, name, status, sort, direction, min);
        return await ReadTeamAsync(from, to, branchId, page, pageSize, filter,
            (query, token) => reads.SalesTeamAsync(HttpContextActor.ActorId(context), query, token), cancellationToken);
    }

    private static async Task<IResult> WorkshopTeamAsync(HttpContext context, IPerformanceReads reads,
        CancellationToken cancellationToken, string? from = null, string? to = null, Guid? branchId = null,
        int page = 1, int pageSize = Pagination.DefaultPageSize, string? search = null, string? name = null,
        string? status = null, string? sort = null, string? direction = null, string? min = null)
    {
        var filter = PerformanceTeamFilter.Parse(search, name, status, sort, direction, min);
        return await ReadTeamAsync(from, to, branchId, page, pageSize, filter,
            (query, token) => reads.WorkshopTeamAsync(HttpContextActor.ActorId(context), query, token),
            cancellationToken);
    }

    private static async Task<IResult> ReadTeamAsync<T>(string? from, string? to, Guid? branchId, int page, int pageSize,
        PerformanceTeamFilter? filter,
        Func<PerformanceTeamQuery, CancellationToken, Task<PerformanceReadResult<PerformanceTeamPage<T>>>> read,
        CancellationToken cancellationToken)
    {
        var query = filter is null ? null :
            PerformanceTeamQuery.Parse(PerformanceQuery.Parse(from, to, branchId), page, pageSize, filter);
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
                PerformanceReadStatus.NotFound => Results.NotFound(),
                _ => Results.Problem(statusCode: 400, title: "Provide a valid performance request.")
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Results.Problem(statusCode: 503, title: "Performance figures are temporarily unavailable.");
        }
    }
}

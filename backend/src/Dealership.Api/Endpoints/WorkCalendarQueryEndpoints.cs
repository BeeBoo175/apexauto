using Dealership.Application.Common;
using Dealership.Application.WorkCalendar;
using static Dealership.Api.Endpoints.HttpContextActor;

namespace Dealership.Api.Endpoints;

public static class WorkCalendarQueryEndpoints
{
    public static void MapWorkCalendarQueryEndpoints(this WebApplication app)
    {
        var routes = app.MapGroup("/api/staff/work-calendar").RequireAuthorization();
        routes.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            return await next(context);
        });
        routes.MapGet("", ScheduledAsync);
        routes.MapGet("/unscheduled", UnscheduledAsync);
    }

    private static Task<IResult> ScheduledAsync(HttpContext context, IWorkCalendarStore reads,
        CancellationToken cancellationToken, string? from = null, string? to = null,
        Guid? branchId = null, string? employee = null, string? search = null, string? assignment = null)
    {
        var query = WorkCalendarQuery.Parse(from, to, branchId, employee, search, assignment);
        return query is null
            ? Task.FromResult<IResult>(Results.Problem(statusCode: 400, title: "Choose a valid calendar period of at most 42 days."))
            : ReadAsync(token => reads.ScheduledAsync(ActorId(context), query, token), cancellationToken);
    }

    private static Task<IResult> UnscheduledAsync(HttpContext context, IWorkCalendarStore reads,
        CancellationToken cancellationToken, Guid? branchId = null, string? employee = null,
        int page = 1, int pageSize = Pagination.DefaultPageSize, string? search = null, string? view = null)
    {
        var query = UnscheduledWorkQuery.Parse(branchId, employee, page, pageSize, search, view);
        return query is null
            ? Task.FromResult<IResult>(Results.Problem(statusCode: 400, title: "Provide valid work filters and paging."))
            : ReadAsync(token => reads.UnscheduledAsync(ActorId(context), query, token), cancellationToken);
    }

    private static async Task<IResult> ReadAsync<T>(Func<CancellationToken, Task<WorkCalendarReadResult<T>>> read,
        CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            var result = await read(deadline.Token);
            return result.Status switch
            {
                WorkCalendarReadStatus.Succeeded => Results.Ok(result.Value),
                WorkCalendarReadStatus.Forbidden => Results.Forbid(),
                WorkCalendarReadStatus.Invalid => Results.Problem(statusCode: 400, title: "Provide valid work filters."),
                _ => Results.Problem(statusCode: 422, title: "More than 1,000 appointments and test drives match. Refine the search or choose another month.")
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Results.Problem(statusCode: 503, title: "Work could not be refreshed.");
        }
    }
}

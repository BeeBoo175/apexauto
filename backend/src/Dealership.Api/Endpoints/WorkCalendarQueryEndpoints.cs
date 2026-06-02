using System.IdentityModel.Tokens.Jwt;
using Dealership.Application.Common;
using Dealership.Application.WorkCalendar;

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
        Guid? branchId = null, string? employee = null)
    {
        var query = WorkCalendarQuery.Parse(from, to, branchId, employee);
        return query is null
            ? Task.FromResult<IResult>(Results.Problem(statusCode: 400, title: "Choose a valid calendar period of at most 42 days."))
            : ReadAsync(token => reads.ScheduledAsync(ActorId(context), query, token), cancellationToken);
    }

    private static Task<IResult> UnscheduledAsync(HttpContext context, IWorkCalendarStore reads,
        CancellationToken cancellationToken, Guid? branchId = null, string? employee = null,
        int page = 1, int pageSize = Pagination.DefaultPageSize)
    {
        var query = UnscheduledWorkQuery.Parse(branchId, employee, page, pageSize);
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
                WorkCalendarReadStatus.BranchRequired => Results.Problem(statusCode: 400, title: "Choose a branch to read its work calendar."),
                _ => Results.Problem(statusCode: 422, title: "More than 1,000 appointments match. Refine the employee filter.")
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Results.Problem(statusCode: 503, title: "Work could not be refreshed.");
        }
    }

    private static Guid ActorId(HttpContext context) => Guid.Parse(context.User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);
}

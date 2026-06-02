using Dealership.Application.Common;
using Dealership.Application.TestDrives;

namespace Dealership.Api.Endpoints;

public static class StaffTestDriveQueryEndpoints
{
    public static void MapStaffTestDriveQueryEndpoints(this WebApplication app)
    {
        var routes = app.MapGroup("/api/staff/test-drives").RequireAuthorization();
        routes.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            return await next(context);
        });
        routes.MapGet("", ListAsync);
        routes.MapGet("/{id:guid}", (Guid id, HttpContext context, IStaffTestDriveReadStore reads, CancellationToken token) =>
            ReadAsync(deadline => reads.GetAsync(TestDriveCommandEndpoints.ActorId(context), id, deadline), token));
        routes.MapGet("/{id:guid}/handlers", (Guid id, HttpContext context, IStaffTestDriveReadStore reads, CancellationToken token) =>
            ReadAsync(deadline => reads.HandlersAsync(TestDriveCommandEndpoints.ActorId(context), id, deadline), token));
    }

    private static Task<IResult> ListAsync(HttpContext context, IStaffTestDriveReadStore reads,
        CancellationToken cancellationToken, string? from = null, string? to = null,
        int page = 1, int pageSize = Pagination.DefaultPageSize, string? queue = null, string? status = null,
        Guid? branchId = null, Guid? handlerId = null, string? search = null, string? sort = null, string? direction = null,
        string? handler = null)
    {
        var query = StaffTestDriveQuery.Parse(from, to, page, pageSize, queue, status, branchId, handlerId, search, sort, direction, handler);
        return query is null
            ? Task.FromResult<IResult>(Results.Problem(statusCode: 400, title: "Provide valid test-drive dates, filters, sort and paging."))
            : ReadAsync(deadline => reads.ListAsync(TestDriveCommandEndpoints.ActorId(context), query, deadline), cancellationToken);
    }

    private static async Task<IResult> ReadAsync<T>(Func<CancellationToken, Task<StaffTestDriveReadResult<T>>> read,
        CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            var result = await read(deadline.Token);
            return result.Status switch
            {
                TestDriveReadStatus.Succeeded => Results.Ok(result.Value),
                TestDriveReadStatus.Forbidden => Results.Forbid(),
                TestDriveReadStatus.NotFound => Results.NotFound(),
                TestDriveReadStatus.Invalid => Results.Problem(statusCode: 400),
                _ => Results.Problem(statusCode: 500)
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Results.Problem(statusCode: 503, title: "Test-drive staff records are temporarily unavailable.");
        }
    }
}

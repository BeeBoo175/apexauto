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
            ReadAsync(deadline => reads.GetAsync(HttpContextActor.ActorId(context), id, deadline), token));
        routes.MapGet("/{id:guid}/handlers", (Guid id, HttpContext context, IStaffTestDriveReadStore reads, CancellationToken token) =>
            ReadAsync(deadline => reads.HandlersAsync(HttpContextActor.ActorId(context), id, deadline), token));
        routes.MapGet("/{id:guid}/history", (Guid id, int? page, int? pageSize, HttpContext context,
            IStaffTestDriveReadStore reads, CancellationToken token) =>
        {
            var (number, size) = (page ?? 1, pageSize ?? Pagination.DefaultPageSize);
            return Pagination.IsValid(number, size)
                ? ReadAsync(deadline => reads.HistoryAsync(HttpContextActor.ActorId(context), id, number, size, deadline), token)
                : Task.FromResult<IResult>(Results.Problem(statusCode: 400, title: "Provide a valid history page."));
        });
    }

    private static Task<IResult> ListAsync(HttpContext context, IStaffTestDriveReadStore reads,
        CancellationToken cancellationToken, string? from = null, string? to = null,
        int page = 1, int pageSize = Pagination.DefaultPageSize, string? queue = null, string? status = null,
        Guid? branchId = null, Guid? handlerId = null, string? search = null, string? sort = null, string? direction = null,
        string? handler = null, string? registration = null, string? vehicle = null, string? customer = null)
    {
        var query = StaffTestDriveQuery.Parse(from, to, page, pageSize, queue, status, branchId, handlerId, search, sort, direction, handler,
            registration, vehicle, customer);
        return query is null
            ? Task.FromResult<IResult>(Results.Problem(statusCode: 400, title: "Provide valid test-drive dates, filters, sort and paging."))
            : ReadAsync(deadline => reads.ListAsync(HttpContextActor.ActorId(context), query, deadline), cancellationToken);
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
                TestDriveReadStatus.Invalid => Results.Problem(statusCode: 400, title: "Provide a valid test-drive request."),
                _ => Results.Problem(statusCode: 500)
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Results.Problem(statusCode: 503, title: "Test-drive staff records are temporarily unavailable.");
        }
    }
}

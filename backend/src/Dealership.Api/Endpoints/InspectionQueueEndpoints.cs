using Dealership.Application.Common;
using Dealership.Application.Inspections;
using static Dealership.Api.Endpoints.HttpContextActor;

namespace Dealership.Api.Endpoints;

public static class InspectionQueueEndpoints
{
    public static void MapInspectionQueueEndpoints(this WebApplication app)
    {
        var routes = app.MapGroup("/api/staff/inspection").RequireAuthorization();
        routes.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            return await next(context);
        });
        routes.MapGet("/vehicles", InspectionsAsync);
        routes.MapGet("/repair-requests", RepairsAsync);
        routes.MapGet("/workshop", WorkshopAsync);
        routes.MapGet("/workshop/jobs", WorkshopJobsAsync);
    }

    private static async Task<IResult> InspectionsAsync(string? from, string? to, string? status, Guid? branchId,
        int? page, int? pageSize, string? search, HttpContext context, IInspectionQueueStore store,
        CancellationToken cancellationToken)
    {
        var filter = InspectionQueueFilter.Parse(from, to, branchId, page ?? 1, pageSize ?? Pagination.DefaultPageSize, search);
        if (filter is null || !Enum.TryParse<InspectionQueueStatus>(status ?? "All", out var state) ||
            !Enum.IsDefined(state) || state.ToString() != (status ?? "All"))
        {
            return Invalid();
        }

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            var result = await store.InspectionsAsync(ActorId(context), filter, state, deadline.Token);
            return Map(result.Status, result.Queue);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Results.Problem(statusCode: 503, title: "Inspection queue is temporarily unavailable.");
        }
    }

    private static async Task<IResult> RepairsAsync(string? from, string? to, string? status, Guid? branchId,
        int? page, int? pageSize, string? search, HttpContext context, IInspectionQueueStore store,
        CancellationToken cancellationToken)
    {
        var filter = InspectionQueueFilter.Parse(from, to, branchId, page ?? 1, pageSize ?? Pagination.DefaultPageSize, search);
        if (filter is null || !Enum.TryParse<InspectionRepairQueueStatus>(status ?? "PendingApproval", out var state) ||
            !Enum.IsDefined(state) || state.ToString() != (status ?? "PendingApproval"))
        {
            return Invalid();
        }

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            var result = await store.RepairsAsync(ActorId(context), filter, state, deadline.Token);
            return Map(result.Status, result.Queue);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Results.Problem(statusCode: 503, title: "Repair approval queue is temporarily unavailable.");
        }
    }

    private static async Task<IResult> WorkshopAsync(string? from, string? to, string? status, Guid? branchId,
        int? page, int? pageSize, string? search, string? sort, string? direction, HttpContext context, IWorkshopQueueStore store,
        string? workStatus, string? technician, decimal? minimumBudget, decimal? maximumBudget,
        string? registration, string? vehicle, CancellationToken cancellationToken)
    {
        var filter = InspectionQueueFilter.Parse(from, to, branchId, page ?? 1, pageSize ?? Pagination.DefaultPageSize,
            search, sort, direction);
        if (filter is null || !Enum.TryParse<WorkshopTask>(status ?? "Active", out var task) ||
            !Enum.IsDefined(task) || task.ToString() != (status ?? "Active"))
        {
            return Invalid();
        }

        filter = filter with
        {
            WorkStatus = workStatus ?? "",
            Technician = technician?.Trim() ?? "",
            MinimumBudget = minimumBudget,
            MaximumBudget = maximumBudget,
            Registration = registration?.Trim() ?? "",
            Vehicle = vehicle?.Trim() ?? ""
        };
        if (!filter.IsValid)
        {
            return Invalid();
        }

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            var result = await store.ReadAsync(ActorId(context), filter, task, deadline.Token);
            return Map(result.Status, result.Queue);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Results.Problem(statusCode: 503, title: "Workshop queue is temporarily unavailable.");
        }
    }

    private static async Task<IResult> WorkshopJobsAsync(string? status, int? page, int? pageSize, string? search,
        HttpContext context, IWorkshopQueueStore store, CancellationToken cancellationToken)
    {
        var filter = InspectionQueueFilter.Parse(null, null, null, page: page ?? 1, pageSize: pageSize ?? Pagination.DefaultPageSize,
            search: search);
        if (filter is null || !Enum.TryParse<WorkshopTask>(status, out var task) ||
            task is not (WorkshopTask.MyWork or WorkshopTask.Waiting or WorkshopTask.Claimable) ||
            task.ToString() != status)
        {
            return Invalid();
        }

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            var result = await store.ReadJobsAsync(ActorId(context), filter, task, deadline.Token);
            return Map(result.Status, result.Queue);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Results.Problem(statusCode: 503, title: "Workshop jobs are temporarily unavailable.");
        }
    }

    private static IResult Invalid() => Results.Problem(statusCode: 400,
        title: "Provide valid queue filters, Malaysian dates and a page size of 10, 25 or 50.");

    private static IResult Map(InspectionAssignmentStatus status, object? queue) => status switch
    {
        InspectionAssignmentStatus.Succeeded => Results.Ok(queue),
        InspectionAssignmentStatus.Invalid => Invalid(),
        InspectionAssignmentStatus.Forbidden => Results.Forbid(),
        _ => Results.Problem(statusCode: 500)
    };
}

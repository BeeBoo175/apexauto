using System.Text.Json;
using Dealership.Api.Configuration;
using Dealership.Application.Common;
using Dealership.Application.Inspections;
using static Dealership.Api.Endpoints.HttpContextActor;

namespace Dealership.Api.Endpoints;

public static class InspectionQueueEndpoints
{
    public static void MapInspectionQueueEndpoints(this WebApplication app)
    {
        var routes = app.MapGroup("/api/staff/inspection").RequireAuthorization().NoStore();
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
        if (filter is null || !TryQueryToken(status ?? "all", out InspectionQueueStatus state))
        {
            return Invalid();
        }

        return await BoundedRead.RunAsync(async token =>
        {
            var result = await store.InspectionsAsync(ActorId(context), filter, state, token);
            return Map(result.Status, result.Queue);
        }, "Inspection queue is temporarily unavailable.", cancellationToken);
    }

    private static async Task<IResult> RepairsAsync(string? from, string? to, string? status, Guid? branchId,
        int? page, int? pageSize, string? search, HttpContext context, IInspectionQueueStore store,
        CancellationToken cancellationToken)
    {
        var filter = InspectionQueueFilter.Parse(from, to, branchId, page ?? 1, pageSize ?? Pagination.DefaultPageSize, search);
        if (filter is null || !TryQueryToken(status ?? "pendingApproval", out InspectionRepairQueueStatus state))
        {
            return Invalid();
        }

        return await BoundedRead.RunAsync(async token =>
        {
            var result = await store.RepairsAsync(ActorId(context), filter, state, token);
            return Map(result.Status, result.Queue);
        }, "Repair approval queue is temporarily unavailable.", cancellationToken);
    }

    private static async Task<IResult> WorkshopAsync(string? from, string? to, string? status, Guid? branchId,
        int? page, int? pageSize, string? search, string? sort, string? direction, HttpContext context, IWorkshopQueueStore store,
        string? workStatus, string? technician, decimal? minimumBudget, decimal? maximumBudget,
        string? registration, string? vehicle, CancellationToken cancellationToken)
    {
        var filter = InspectionQueueFilter.Parse(from, to, branchId, page ?? 1, pageSize ?? Pagination.DefaultPageSize,
            search, sort, direction);
        if (filter is null || !TryQueryToken(status ?? "active", out WorkshopTask task))
        {
            return Invalid();
        }

        var selectedWorkStatus = "";
        if (!string.IsNullOrEmpty(workStatus))
        {
            if (!TryQueryToken(workStatus, out WorkshopWorkStatus state))
            {
                return Invalid();
            }
            selectedWorkStatus = state.ToString();
        }

        filter = filter with
        {
            WorkStatus = selectedWorkStatus,
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

        return await BoundedRead.RunAsync(async token =>
        {
            var result = await store.ReadAsync(ActorId(context), filter, task, token);
            return Map(result.Status, result.Queue);
        }, "Workshop queue is temporarily unavailable.", cancellationToken);
    }

    private static async Task<IResult> WorkshopJobsAsync(string? status, int? page, int? pageSize, string? search,
        HttpContext context, IWorkshopQueueStore store, CancellationToken cancellationToken)
    {
        var filter = InspectionQueueFilter.Parse(null, null, null, page: page ?? 1, pageSize: pageSize ?? Pagination.DefaultPageSize,
            search: search);
        if (filter is null || !TryQueryToken(status, out WorkshopTask task) ||
            task is not (WorkshopTask.MyWork or WorkshopTask.Waiting or WorkshopTask.Claimable))
        {
            return Invalid();
        }

        return await BoundedRead.RunAsync(async token =>
        {
            var result = await store.ReadJobsAsync(ActorId(context), filter, task, token);
            return Map(result.Status, result.Queue);
        }, "Workshop jobs are temporarily unavailable.", cancellationToken);
    }

    private static bool TryQueryToken<T>(string? token, out T value) where T : struct, Enum
    {
        return Enum.TryParse(token, true, out value) && Enum.IsDefined(value) &&
            JsonNamingPolicy.CamelCase.ConvertName(value.ToString()) == token;
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

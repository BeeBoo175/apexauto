using Dealership.Application.Accounts;
using Dealership.Application.Common;

namespace Dealership.Api.Endpoints;

public static class StaffAccountDirectoryEndpoints
{
    public static void MapStaffAccountDirectoryEndpoints(this WebApplication app)
    {
        var routes = app.MapGroup("/api/staff-accounts").RequireAuthorization();
        routes.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            return await next(context);
        });
        routes.MapGet("", ListAsync);
        routes.MapGet("/{id:guid}/history", HistoryAsync);
    }

    private static async Task<IResult> HistoryAsync(Guid id, HttpContext context, IStaffAccountDirectory directory,
        CancellationToken cancellationToken, int page = 1, int pageSize = Pagination.DefaultPageSize)
    {
        if (!Pagination.IsValid(page, pageSize))
        {
            return Results.Problem(statusCode: 400, title: "Provide a valid history page.");
        }

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            var result = await directory.HistoryAsync(HttpContextActor.ActorId(context), id, page, pageSize, deadline.Token);
            return result.Status switch
            {
                StaffAccountReadStatus.Succeeded => Results.Ok(result.Page),
                StaffAccountReadStatus.Forbidden => Results.Forbid(),
                StaffAccountReadStatus.NotFound => Results.NotFound(),
                _ => Results.Problem(statusCode: 400, title: "Provide a valid history page.")
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Results.Problem(statusCode: 503, title: "Account history is temporarily unavailable.");
        }
    }

    private static async Task<IResult> ListAsync(HttpContext context, IStaffAccountDirectory directory,
        CancellationToken cancellationToken, string? tab = null, string? search = null, string? role = null,
        Guid? branchId = null, int page = 1, int pageSize = Pagination.DefaultPageSize)
    {
        var query = StaffAccountQuery.Parse(tab, search, role, branchId, page, pageSize);
        if (query is null)
        {
            return Results.Problem(statusCode: 400, title: "Provide a valid staff account tab, search, filters and paging.");
        }

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            var result = await directory.ListAsync(HttpContextActor.ActorId(context), query, deadline.Token);
            return result.Status switch
            {
                StaffAccountReadStatus.Succeeded => Results.Ok(result.Page),
                StaffAccountReadStatus.Forbidden => Results.Forbid(),
                _ => Results.Problem(statusCode: 400, title: "Provide a valid staff account request.")
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Results.Problem(statusCode: 503, title: "Staff accounts are temporarily unavailable.");
        }
    }
}

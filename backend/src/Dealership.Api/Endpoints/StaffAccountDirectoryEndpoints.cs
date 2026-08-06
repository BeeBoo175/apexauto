using Dealership.Api.Configuration;
using Dealership.Application.Accounts;
using Dealership.Application.Common;

namespace Dealership.Api.Endpoints;

public static class StaffAccountDirectoryEndpoints
{
    public static void MapStaffAccountDirectoryEndpoints(this WebApplication app)
    {
        var routes = app.MapGroup("/api/staff-accounts").RequireAuthorization().NoStore();
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

        return await BoundedRead.RunAsync(async token =>
        {
            var result = await directory.HistoryAsync(HttpContextActor.ActorId(context), id, page, pageSize, token);
            return result.Status switch
            {
                StaffAccountReadStatus.Succeeded => Results.Ok(result.Page),
                StaffAccountReadStatus.Forbidden => Results.Forbid(),
                StaffAccountReadStatus.NotFound => Results.NotFound(),
                _ => Results.Problem(statusCode: 400, title: "Provide a valid history page.")
            };
        }, "Account history is temporarily unavailable.", cancellationToken);
    }

    private static async Task<IResult> ListAsync(HttpContext context, IStaffAccountDirectory directory,
        CancellationToken cancellationToken, string? view = null, string? search = null, string? role = null,
        Guid? branchId = null, int page = 1, int pageSize = Pagination.DefaultPageSize, string? name = null,
        string? email = null, string? mobile = null)
    {
        var query = StaffAccountQuery.Parse(view, search, role, branchId, page, pageSize, name, email, mobile);
        if (query is null)
        {
            return Results.Problem(statusCode: 400, title: "Provide a valid staff account view, search, filters and paging.");
        }

        return await BoundedRead.RunAsync(async token =>
        {
            var result = await directory.ListAsync(HttpContextActor.ActorId(context), query, token);
            return result.Status switch
            {
                StaffAccountReadStatus.Succeeded => Results.Ok(result.Page),
                StaffAccountReadStatus.Forbidden => Results.Forbid(),
                _ => Results.Problem(statusCode: 400, title: "Provide a valid staff account request.")
            };
        }, "Staff accounts are temporarily unavailable.", cancellationToken);
    }
}

using Dealership.Api.Configuration;
using Dealership.Application.Accounts;
using Dealership.Application.Common;

namespace Dealership.Api.Endpoints;

public static class CustomerAccountDirectoryEndpoints
{
    public static void MapCustomerAccountDirectoryEndpoints(this WebApplication app)
    {
        var routes = app.MapGroup("/api/customer-accounts").RequireAuthorization().NoStore();
        routes.MapGet("", ListAsync);
        routes.MapGet("/{id:guid}", GetAsync);
    }

    private static async Task<IResult> ListAsync(HttpContext context, ICustomerAccountDirectory directory,
        CancellationToken cancellationToken, string? tab = null, string? search = null, int page = 1,
        int pageSize = Pagination.DefaultPageSize, string? name = null, string? email = null,
        string? mobile = null)
    {
        var query = CustomerDirectoryQuery.Parse(tab, search, page, pageSize, name, email, mobile);
        if (query is null)
        {
            return Results.Problem(statusCode: 400, title: "Provide a valid customer account tab, search and paging.");
        }

        return await BoundedRead.RunAsync(async token =>
        {
            return Map(await directory.ListAsync(HttpContextActor.ActorId(context), query, token));
        }, "Customer accounts are temporarily unavailable.", cancellationToken);
    }

    private static async Task<IResult> GetAsync(Guid id, HttpContext context, ICustomerAccountDirectory directory,
        CancellationToken cancellationToken)
    {
        return await BoundedRead.RunAsync(async token =>
        {
            return Map(await directory.GetAsync(HttpContextActor.ActorId(context), id, token));
        }, "Customer account details are temporarily unavailable.", cancellationToken);
    }

    private static IResult Map<T>(CustomerDirectoryResult<T> result) => result.Status switch
    {
        StaffAccountReadStatus.Succeeded => Results.Ok(result.Value),
        StaffAccountReadStatus.Forbidden => Results.Forbid(),
        StaffAccountReadStatus.NotFound => Results.NotFound(),
        _ => Results.Problem(statusCode: 400, title: "Provide a valid customer account request.")
    };
}

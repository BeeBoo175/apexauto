using Dealership.Api.Configuration;
using Dealership.Application.Accounts;

namespace Dealership.Api.Endpoints;

public static class StaffAssistanceEndpoints
{
    public static void MapStaffAssistanceEndpoints(this WebApplication app)
    {
        var routes = app.MapGroup("/api/staff/customer-lookup").RequireAuthorization().NoStore();
        routes.MapGet("", SearchAsync);
    }

    private static async Task<IResult> SearchAsync(HttpContext context, IStaffAssistanceQuery assistance,
        CancellationToken cancellationToken, string? search = null)
    {
        var query = CustomerLookupQuery.Parse(search);
        if (query is null)
        {
            return Results.Problem(statusCode: 400,
                title: $"Search by at least {CustomerLookupQuery.MinimumSearchLength} characters of a name, email or mobile.");
        }

        return await BoundedRead.RunAsync(async token =>
        {
            var result = await assistance.SearchCustomersAsync(HttpContextActor.ActorId(context), query, token);
            return result.Status == StaffAccountReadStatus.Succeeded
                ? Results.Ok(new { items = result.Items })
                : Results.Forbid();
        }, "Customer lookup is temporarily unavailable.", cancellationToken);
    }
}

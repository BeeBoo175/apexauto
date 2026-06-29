using Dealership.Application.Accounts;

namespace Dealership.Api.Endpoints;

public static class AccountEditEndpoints
{
    public static void MapAccountEditEndpoints(this WebApplication app)
    {
        var routes = app.MapGroup("/api/accounts/{id:guid}/edits").RequireAuthorization();
        routes.MapPost("", EditAsync);
    }

    private static async Task<IResult> EditAsync(Guid id, AccountEditRequest request, HttpContext context,
        AccountEditService edits, CancellationToken cancellationToken)
    {
        var status = await edits.EditAsync(HttpContextActor.ActorId(context), id, request, cancellationToken);
        context.Response.Headers.CacheControl = "no-store";
        return status switch
        {
            AccountEditStatus.Succeeded => Results.NoContent(),
            AccountEditStatus.Invalid => Results.Problem(statusCode: 400,
                title: "Provide an operation id, a reason of at most 1000 characters and valid details."),
            AccountEditStatus.Forbidden => Results.Forbid(),
            AccountEditStatus.NotFound => Results.NotFound(),
            _ => Results.Conflict()
        };
    }
}

using Dealership.Api.Configuration;
using Dealership.Application.Accounts;

namespace Dealership.Api.Endpoints;

public static class AccountSuspensionEndpoints
{
    public static void MapAccountSuspensionEndpoints(this WebApplication app)
    {
        var routes = app.MapGroup("/api/accounts/{id:guid}/suspensions").RequireAuthorization().NoStore();
        routes.MapPost("", SuspendAsync);
        routes.MapPost("/lift", LiftAsync);
    }

    private static async Task<IResult> SuspendAsync(Guid id, AccountSuspensionRequest request, HttpContext context,
        AccountSuspensionService suspensions, CancellationToken cancellationToken)
    {
        return Map(await suspensions.SuspendAsync(HttpContextActor.ActorId(context), id, request, cancellationToken));
    }

    private static async Task<IResult> LiftAsync(Guid id, AccountSuspensionRequest request, HttpContext context,
        AccountSuspensionService suspensions, CancellationToken cancellationToken)
    {
        return Map(await suspensions.LiftAsync(HttpContextActor.ActorId(context), id, request, cancellationToken));
    }

    private static IResult Map(AccountSuspensionStatus status)
    {
        return status switch
        {
            AccountSuspensionStatus.Succeeded => Results.NoContent(),
            AccountSuspensionStatus.Invalid => Results.Problem(statusCode: 400,
                title: "Provide an operation id and, to suspend, a reason of at most 1000 characters."),
            AccountSuspensionStatus.Forbidden => Results.Forbid(),
            AccountSuspensionStatus.NotFound => Results.NotFound(),
            _ => Results.Conflict()
        };
    }
}

using Dealership.Api.Configuration;
using Dealership.Application.Accounts;

namespace Dealership.Api.Endpoints;

public static class CustomerClosureEndpoints
{
    public static void MapCustomerClosureEndpoints(this WebApplication app)
    {
        var routes = app.MapGroup("/api/account/closure").RequireAuthorization().NoStore();
        routes.MapGet("/blockers", BlockersAsync);
        // Password re-entry is a guessing surface, so it shares the login budget.
        routes.MapPost("", CloseAsync).RequireRateLimiting(ApiRateLimiting.LoginPolicy);
    }

    private static async Task<IResult> BlockersAsync(HttpContext context, CustomerClosureService service,
        CancellationToken cancellationToken)
    {
        return await BoundedRead.RunAsync(async token =>
        {
            var blockers = await service.BlockersAsync(HttpContextActor.ActorId(context), token);
            return blockers is null ? Results.Forbid() : Results.Ok(new { blockers });
        }, "Account closure details are temporarily unavailable.", cancellationToken);
    }

    // A wrong password answers 409 with a code, never 401, because 401 signs the browser out.
    private static async Task<IResult> CloseAsync(CustomerClosureRequest request, HttpContext context,
        CustomerClosureService service, CancellationToken cancellationToken)
    {
        var result = await service.CloseAsync(HttpContextActor.ActorId(context), request, cancellationToken);
        return result.Status switch
        {
            CustomerClosureStatus.Closed => Results.Ok(result.Receipt),
            CustomerClosureStatus.Forbidden => Results.Forbid(),
            CustomerClosureStatus.WrongPassword => Results.Json(new { code = "wrong-password" }, statusCode: 409),
            CustomerClosureStatus.ProtectedSample => Results.Json(new { code = "protected-sample" }, statusCode: 409),
            CustomerClosureStatus.SessionChanged => Results.Json(new { code = "session-changed" }, statusCode: 409),
            CustomerClosureStatus.RetryLater => Results.Json(new { code = "retry-later" }, statusCode: 409),
            CustomerClosureStatus.Blocked => Results.Json(new { code = "blocked", blockers = result.Blockers },
                statusCode: 409),
            _ => Results.Problem(statusCode: 400, title: "Provide an operation id and your password.")
        };
    }
}

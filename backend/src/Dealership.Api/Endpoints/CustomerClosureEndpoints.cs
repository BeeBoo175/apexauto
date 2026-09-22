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
            // The retained categories travel with the blockers so the confirmation can name them before the customer acts.
            if (blockers is null)
            {
                return Results.Forbid();
            }

            var retained = await service.RetainedAsync(token);
            return Results.Ok(new { blockers, retained });
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
            CustomerClosureStatus.Conflict => CodedProblem.CodeConflict("conflict"),
            CustomerClosureStatus.WrongPassword => CodedProblem.CodeConflict("wrong-password"),
            CustomerClosureStatus.ProtectedSample => CodedProblem.CodeConflict("protected-sample"),
            CustomerClosureStatus.SessionChanged => CodedProblem.CodeConflict("session-changed"),
            CustomerClosureStatus.RetryLater => CodedProblem.CodeConflict("retry-later"),
            CustomerClosureStatus.Blocked => CodedProblem.Blocked(result.Blockers),
            _ => Results.Problem(statusCode: 400, title: "Provide an operation id and your password.")
        };
    }
}

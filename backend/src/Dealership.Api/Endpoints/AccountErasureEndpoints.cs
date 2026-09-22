using Dealership.Api.Configuration;
using Dealership.Application.Accounts;

namespace Dealership.Api.Endpoints;

// Administrator erasure of a customer account. The Administrator's password is a guessing surface, so the command
// shares the login budget, and a wrong password answers 409 because a 401 would sign the Administrator out.
public static class AccountErasureEndpoints
{
    public static void MapAccountErasureEndpoints(this WebApplication app)
    {
        var routes = app.MapGroup("/api/accounts/{id:guid}").RequireAuthorization().NoStore();
        routes.MapGet("/erasure-blockers", BlockersAsync);
        routes.MapPost("/erasures", EraseAsync).RequireRateLimiting(ApiRateLimiting.LoginPolicy);
    }

    private static async Task<IResult> BlockersAsync(Guid id, HttpContext context, AccountErasureService service,
        CancellationToken cancellationToken)
    {
        return await BoundedRead.RunAsync(async token =>
        {
            var administratorId = HttpContextActor.ActorId(context);
            if (!await service.IsAdministratorAsync(administratorId, token))
            {
                return Results.Forbid();
            }

            var preview = await service.PreviewCustomerAsync(administratorId, id, token);
            // The retained categories travel with the blockers so the confirmation names them before the Administrator acts.
            if (preview is null)
            {
                return Results.NotFound();
            }

            var retained = await service.RetainedAsync(token);
            return Results.Ok(new { blockers = preview.CustomerBlockers, retained });
        }, "Account erasure details are temporarily unavailable.", cancellationToken);
    }

    private static async Task<IResult> EraseAsync(Guid id, AccountErasureRequest request, HttpContext context,
        AccountErasureService service, CancellationToken cancellationToken)
    {
        var result = await service.EraseCustomerAsync(HttpContextActor.ActorId(context), id, request, cancellationToken);
        return result.Status switch
        {
            AccountErasureStatus.Erased => Results.Ok(result.Receipt),
            AccountErasureStatus.Forbidden => Results.Forbid(),
            AccountErasureStatus.NotFound => Results.NotFound(),
            AccountErasureStatus.WrongPassword => Results.Json(new { code = "wrong-password" }, statusCode: 409),
            AccountErasureStatus.Refused => Results.Json(new { code = "protected" }, statusCode: 409),
            AccountErasureStatus.Conflict => Results.Json(new { code = "conflict" }, statusCode: 409),
            AccountErasureStatus.SessionChanged => Results.Json(new { code = "session-changed" }, statusCode: 409),
            AccountErasureStatus.RetryLater => Results.Json(new { code = "retry-later" }, statusCode: 409),
            AccountErasureStatus.Blocked => Results.Json(new { code = "blocked", blockers = result.CustomerBlockers },
                statusCode: 409),
            _ => Results.Problem(statusCode: 400,
                title: "Provide an operation id, a reason of at most 1000 characters and your password.")
        };
    }
}

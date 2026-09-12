using Dealership.Api.Configuration;
using Dealership.Application.Accounts;

namespace Dealership.Api.Endpoints;

public static class StaffAccountAdministrationEndpoints
{
    public static void MapStaffAccountAdministrationEndpoints(this WebApplication app)
    {
        var routes = app.MapGroup("/api/staff-accounts/{id:guid}").RequireAuthorization().NoStore();
        routes.MapGet("/blockers", BlockersAsync);
        routes.MapPost("/deactivations", (Guid id, AccountDeactivationRequest request, HttpContext context,
            StaffAccountAdministrationService service, CancellationToken token) => RunAsync(
            () => service.DeactivateAsync(HttpContextActor.ActorId(context), id, request, token)));
        routes.MapPost("/reactivations", (Guid id, AccountReactivationRequest request, HttpContext context,
            StaffAccountAdministrationService service, CancellationToken token) => RunAsync(
            () => service.ReactivateAsync(HttpContextActor.ActorId(context), id, request, token)));
        // The Administrator's password is a guessing surface, so erasure shares the login budget.
        routes.MapPost("/erasures", EraseAsync).RequireRateLimiting(ApiRateLimiting.LoginPolicy);
        routes.MapPost("/transfers", (Guid id, AccountTransferRequest request, HttpContext context,
            StaffAccountAdministrationService service, CancellationToken token) => RunAsync(
            () => service.TransferAsync(HttpContextActor.ActorId(context), id, request, token)));
    }

    private static async Task<IResult> BlockersAsync(Guid id, HttpContext context,
        StaffAccountAdministrationService service, CancellationToken cancellationToken)
    {
        return await BoundedRead.RunAsync(async token =>
        {
            var result = await service.BlockersAsync(HttpContextActor.ActorId(context), id, token);
            return result.Status switch
            {
                AccountAdministrationStatus.Succeeded => Results.Ok(new { blockers = result.Blockers }),
                AccountAdministrationStatus.Forbidden => Results.Forbid(),
                AccountAdministrationStatus.NotFound => Results.NotFound(),
                _ => Results.Problem(statusCode: 400, title: "Provide a valid staff account.")
            };
        }, "Staff account blockers are temporarily unavailable.", cancellationToken);
    }

    private static async Task<IResult> EraseAsync(Guid id, AccountErasureRequest request, HttpContext context,
        AccountErasureService service, CancellationToken cancellationToken)
    {
        var result = await service.EraseStaffAsync(HttpContextActor.ActorId(context), id, request, cancellationToken);
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
            AccountErasureStatus.Blocked => Results.Json(new { code = "blocked", blockers = result.StaffBlockers },
                statusCode: 409),
            _ => Results.Problem(statusCode: 400,
                title: "Provide an operation id, a reason of at most 1000 characters and your password.")
        };
    }

    private static async Task<IResult> RunAsync(Func<Task<AccountAdministrationResult>> action)
    {
        var result = await action();
        return result.Status switch
        {
            AccountAdministrationStatus.Succeeded => Results.NoContent(),
            AccountAdministrationStatus.Forbidden => Results.Forbid(),
            AccountAdministrationStatus.NotFound => Results.NotFound(),
            AccountAdministrationStatus.Blocked => Results.Json(new { code = "blocked", blockers = result.Blockers },
                statusCode: 409),
            AccountAdministrationStatus.Conflict => Results.Json(new { code = "conflict" }, statusCode: 409),
            _ => Results.Problem(statusCode: 400,
                title: "Provide an operation id, a reason of at most 1000 characters and, for a transfer, a branch.")
        };
    }
}

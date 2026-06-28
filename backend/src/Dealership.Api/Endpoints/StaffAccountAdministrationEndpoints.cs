using Dealership.Application.Accounts;

namespace Dealership.Api.Endpoints;

public static class StaffAccountAdministrationEndpoints
{
    public static void MapStaffAccountAdministrationEndpoints(this WebApplication app)
    {
        var routes = app.MapGroup("/api/staff-accounts/{id:guid}").RequireAuthorization();
        routes.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            return await next(context);
        });
        routes.MapGet("/blockers", BlockersAsync);
        routes.MapPost("/deactivations", (Guid id, AccountDeactivationRequest request, HttpContext context,
            StaffAccountAdministrationService service, CancellationToken token) => RunAsync(
            () => service.DeactivateAsync(HttpContextActor.ActorId(context), id, request, token)));
        routes.MapPost("/reactivations", (Guid id, AccountReactivationRequest request, HttpContext context,
            StaffAccountAdministrationService service, CancellationToken token) => RunAsync(
            () => service.ReactivateAsync(HttpContextActor.ActorId(context), id, request, token)));
        routes.MapPost("/transfers", (Guid id, AccountTransferRequest request, HttpContext context,
            StaffAccountAdministrationService service, CancellationToken token) => RunAsync(
            () => service.TransferAsync(HttpContextActor.ActorId(context), id, request, token)));
    }

    private static async Task<IResult> BlockersAsync(Guid id, HttpContext context,
        StaffAccountAdministrationService service, CancellationToken cancellationToken)
    {
        var result = await service.BlockersAsync(HttpContextActor.ActorId(context), id, cancellationToken);
        return result.Status switch
        {
            AccountAdministrationStatus.Succeeded => Results.Ok(new { blockers = result.Blockers }),
            AccountAdministrationStatus.Forbidden => Results.Forbid(),
            AccountAdministrationStatus.NotFound => Results.NotFound(),
            _ => Results.Problem(statusCode: 400, title: "Provide a valid staff account.")
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
            AccountAdministrationStatus.Blocked => Results.Json(new { code = "Blocked", blockers = result.Blockers },
                statusCode: 409),
            AccountAdministrationStatus.Conflict => Results.Json(new { code = "Conflict" }, statusCode: 409),
            _ => Results.Problem(statusCode: 400,
                title: "Provide an operation id, a reason of at most 1000 characters and, for a transfer, a branch.")
        };
    }
}

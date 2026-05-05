using System.IdentityModel.Tokens.Jwt;
using Dealership.Application.Sales;

namespace Dealership.Api.Endpoints;

public static class SaleCommandEndpoints
{
    public static void MapSaleCommandEndpoints(this WebApplication app)
    {
        var routes = app.MapGroup("/api/staff/sales/{id:guid}").RequireAuthorization();
        routes.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            return await next(context);
        });
        routes.MapPost("/claims", (Guid id, HttpContext context, SaleHandlingService sales, CancellationToken token) =>
            RunAsync<SaleCommandRequest>(context, token, (actor, request, deadline) =>
                sales.ClaimAsync(actor, id, request, deadline)));
        routes.MapPost("/assignments", (Guid id, HttpContext context, SaleHandlingService sales, CancellationToken token) =>
            RunAsync<SaleAssignmentRequest>(context, token, (actor, request, deadline) =>
                sales.AssignAsync(actor, id, request, deadline)));
        routes.MapPost("/releases", (Guid id, HttpContext context, SaleHandlingService sales, CancellationToken token) =>
            RunAsync<SaleCommandRequest>(context, token, (actor, request, deadline) =>
                sales.ReleaseAsync(actor, id, request, deadline)));
        routes.MapPost("/terms", (Guid id, HttpContext context, SaleTermsService sales, CancellationToken token) =>
            RunAsync<SaleTermsRequest>(context, token, (actor, request, deadline) =>
                sales.SaveTermsAsync(actor, id, request, deadline)));
        routes.MapPost("/submissions", (Guid id, HttpContext context, SaleTermsService sales, CancellationToken token) =>
            RunAsync<SaleCommandRequest>(context, token, (actor, request, deadline) =>
                sales.SubmitAsync(actor, id, request, deadline)));
        routes.MapPost("/approvals", (Guid id, HttpContext context, SaleTermsService sales, CancellationToken token) =>
            RunAsync<SaleCommandRequest>(context, token, (actor, request, deadline) =>
                sales.ApproveAsync(actor, id, request, deadline)));
        routes.MapPost("/rejections", (Guid id, HttpContext context, SaleTermsService sales, CancellationToken token) =>
            RunAsync<SaleReasonRequest>(context, token, (actor, request, deadline) =>
                sales.RejectAsync(actor, id, request, deadline)));
        routes.MapPost("/cancellations", (Guid id, HttpContext context, SaleTermsService sales, CancellationToken token) =>
            RunAsync<SaleReasonRequest>(context, token, (actor, request, deadline) =>
                sales.CancelAsync(actor, id, request, deadline)));
    }

    private static async Task<IResult> RunAsync<TRequest>(HttpContext context, CancellationToken cancellationToken,
        Func<Guid, TRequest, CancellationToken, Task<SaleCommandResult>> command) where TRequest : class
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            var (request, error) = await JsonRequestReader.ReadAsync<TRequest>(context.Request, deadline.Token);
            if (error is not null)
            {
                return error;
            }

            var result = await command(ActorId(context), request!, deadline.Token);
            return result.Status switch
            {
                SaleStatus.Succeeded => Results.Ok(result.Receipt),
                SaleStatus.Invalid => Results.Problem(statusCode: 400, title: "Provide a valid sale command."),
                SaleStatus.Forbidden => Results.Forbid(),
                SaleStatus.NotFound => Results.NotFound(),
                SaleStatus.Conflict => Results.Problem(statusCode: 409,
                    title: "The sale changed or this operation was already used for different contents."),
                SaleStatus.Unavailable => Results.Problem(statusCode: 503, title: "Sale terms are temporarily unavailable."),
                _ => Results.Problem(statusCode: 500)
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Results.Problem(statusCode: 503,
                title: "The sale command is unconfirmed; reconcile with the same operation and contents.");
        }
    }

    private static Guid ActorId(HttpContext context) =>
        Guid.Parse(context.User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);
}

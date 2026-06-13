using Dealership.Application.Sales;
using static Dealership.Api.Endpoints.HttpContextActor;

namespace Dealership.Api.Endpoints;

public static class SaleCommandEndpoints
{
    public static void MapSaleCommandEndpoints(this WebApplication app)
    {
        app.MapSaleDocumentCorrectionEndpoints();
        var routes = app.MapGroup("/api/staff/sales/{id:guid}").RequireAuthorization();
        routes.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            return await next(context);
        });
        routes.MapPost("/claims", (Guid id, HttpContext context, SaleHandlingService sales, CancellationToken token) =>
            RunAsync<SaleCommandRequest>(context, (actor, request, deadline) =>
                sales.ClaimAsync(actor, id, request, deadline), token));
        routes.MapPost("/assignments", (Guid id, HttpContext context, SaleHandlingService sales, CancellationToken token) =>
            RunAsync<SaleAssignmentRequest>(context, (actor, request, deadline) =>
                sales.AssignAsync(actor, id, request, deadline), token));
        routes.MapPost("/releases", (Guid id, HttpContext context, SaleHandlingService sales, CancellationToken token) =>
            RunAsync<SaleCommandRequest>(context, (actor, request, deadline) =>
                sales.ReleaseAsync(actor, id, request, deadline), token));
        routes.MapPost("/term-revisions", (Guid id, HttpContext context, SaleTermsService sales, CancellationToken token) =>
            RunAsync<SaleTermsRequest>(context, (actor, request, deadline) =>
                sales.SaveTermsAsync(actor, id, request, deadline), token));
        routes.MapPost("/submissions", (Guid id, HttpContext context, SaleTermsService sales, CancellationToken token) =>
            RunAsync<SaleCommandRequest>(context, (actor, request, deadline) =>
                sales.SubmitAsync(actor, id, request, deadline), token));
        routes.MapPost("/approvals", (Guid id, HttpContext context, SaleTermsService sales, CancellationToken token) =>
            RunAsync<SaleCommandRequest>(context, (actor, request, deadline) =>
                sales.ApproveAsync(actor, id, request, deadline), token));
        routes.MapPost("/rejections", (Guid id, HttpContext context, SaleTermsService sales, CancellationToken token) =>
            RunAsync<SaleReasonRequest>(context, (actor, request, deadline) =>
                sales.RejectAsync(actor, id, request, deadline), token));
        routes.MapPost("/cancellations", (Guid id, HttpContext context, SaleTermsService sales, CancellationToken token) =>
            RunAsync<SaleReasonRequest>(context, (actor, request, deadline) =>
                sales.CancelAsync(actor, id, request, deadline), token));
        routes.MapPost("/handovers", (Guid id, HttpContext context, SaleHandoverService sales, CancellationToken token) =>
            RunAsync<SaleHandoverInput>(context, (actor, input, deadline) => input.Command() is { } request
                ? sales.DeliverAsync(actor, id, request, deadline)
                : Task.FromResult(new SaleCommandResult(SaleResultStatus.Invalid)), token));
        routes.MapPost("/settlements", (Guid id, HttpContext context, SaleSettlementService sales, CancellationToken token) =>
            RunAsync<SaleSettlementInput>(context, (actor, input, deadline) => input.Command() is { } request
                ? sales.SettleAsync(actor, id, request, deadline)
                : Task.FromResult(new SaleCommandResult(SaleResultStatus.Invalid)), token));
    }

    private static async Task<IResult> RunAsync<TRequest>(HttpContext context,
        Func<Guid, TRequest, CancellationToken, Task<SaleCommandResult>> command,
        CancellationToken cancellationToken) where TRequest : class
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
                SaleResultStatus.Succeeded => Results.Ok(result.Receipt),
                SaleResultStatus.Invalid => Results.Problem(statusCode: 400, title: "Provide a valid sale command."),
                SaleResultStatus.Forbidden => Results.Forbid(),
                SaleResultStatus.NotFound => Results.NotFound(),
                SaleResultStatus.Conflict => Results.Problem(statusCode: 409,
                    title: "The sale changed or this operation was already used for different contents."),
                SaleResultStatus.Unavailable => Results.Problem(statusCode: 503, title: "Sale terms are temporarily unavailable."),
                _ => Results.Problem(statusCode: 500)
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Results.Problem(statusCode: 503,
                title: "The sale command is unconfirmed; reconcile with the same operation and contents.");
        }
    }
}

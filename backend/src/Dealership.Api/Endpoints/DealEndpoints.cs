using System.IdentityModel.Tokens.Jwt;
using Dealership.Application.Common;
using Dealership.Application.Sales;

namespace Dealership.Api.Endpoints;

public static class DealEndpoints
{
    public static void MapDealEndpoints(this WebApplication app)
    {
        var routes = app.MapGroup("/api/staff/deals").RequireAuthorization();
        routes.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            return await next(context);
        });
        routes.MapGet("", ListAsync);
        routes.MapGet("/vehicles", VehiclesAsync);
        routes.MapGet("/{dealId:guid}", GetAsync);
        routes.MapGet("/{dealId:guid}/options", OptionsAsync);
        routes.MapPost("", CreateAsync);
        routes.MapPut("/{dealId:guid}", ReviseAsync);
        routes.MapPost("/{dealId:guid}/submissions", (Guid dealId, HttpContext context, DealService deals,
            CancellationToken token) => TransitionAsync(dealId, DealAction.Submit, context, deals, token));
        routes.MapPost("/{dealId:guid}/approvals", (Guid dealId, HttpContext context, DealService deals,
            CancellationToken token) => TransitionAsync(dealId, DealAction.Approve, context, deals, token));
        routes.MapPost("/{dealId:guid}/rejections", (Guid dealId, HttpContext context, DealService deals,
            CancellationToken token) => TransitionAsync(dealId, DealAction.Reject, context, deals, token));
        routes.MapPost("/{dealId:guid}/cancellations", (Guid dealId, HttpContext context, DealService deals,
            CancellationToken token) => TransitionAsync(dealId, DealAction.Cancel, context, deals, token));
    }

    internal sealed record DealListParameters(int? Page, int? PageSize, string? View, string? Status, string? Sort,
        string? Direction, string? Number, string? Vehicle, string? Customer, string? Search, string? Agent,
        string? Branch, string? NextStep, decimal? MinimumBalance, decimal? MaximumBalance, decimal? MinimumDiscount,
        decimal? MaximumDiscount, string? SubmittedFrom, string? SubmittedTo, string? UpdatedFrom, string? UpdatedTo,
        string? Registration);

    private static Task<IResult> ListAsync([AsParameters] DealListParameters query, HttpContext context,
        DealService deals, CancellationToken cancellationToken) =>
        RunAsync(cancellationToken, "Deals are temporarily unavailable.", token => deals.ListAsync(ActorId(context),
            query.Page ?? 1, query.PageSize ?? Pagination.DefaultPageSize, query.View, query.Status, query.Sort,
            query.Direction, query.Number, query.Vehicle, query.Customer, query.Search,
            new DealColumnFilters(query.Agent, query.Branch, query.NextStep, query.MinimumBalance,
                query.MaximumBalance, query.MinimumDiscount, query.MaximumDiscount, query.SubmittedFrom,
                query.SubmittedTo, query.UpdatedFrom, query.UpdatedTo, query.Registration), token));

    private static Task<IResult> VehiclesAsync(string? search, HttpContext context, DealService deals,
        CancellationToken cancellationToken) =>
        RunAsync(cancellationToken, "Eligible vehicles are temporarily unavailable.",
            token => deals.VehiclesAsync(ActorId(context), search, token));

    private static Task<IResult> OptionsAsync(Guid dealId, HttpContext context, DealService deals,
        CancellationToken cancellationToken) =>
        RunAsync(cancellationToken, "Deal options are temporarily unavailable.",
            token => deals.OptionsAsync(ActorId(context), dealId, token));
    private static Task<IResult> GetAsync(Guid dealId, HttpContext context, DealService deals,
        CancellationToken cancellationToken) =>
        RunAsync(cancellationToken, "The deal is temporarily unavailable.",
            token => deals.GetAsync(ActorId(context), dealId, token));

    private static Task<IResult> CreateAsync(HttpContext context, DealService deals, CancellationToken cancellationToken) =>
        BodyAsync<DealCreateRequest>(context, cancellationToken,
            (request, token) => deals.CreateAsync(ActorId(context), request, token));

    private static Task<IResult> ReviseAsync(Guid dealId, HttpContext context, DealService deals,
        CancellationToken cancellationToken) =>
        BodyAsync<DealReviseRequest>(context, cancellationToken,
            (request, token) => deals.ReviseAsync(ActorId(context), dealId, request, token));

    private static Task<IResult> TransitionAsync(Guid dealId, DealAction action, HttpContext context, DealService deals,
        CancellationToken cancellationToken) =>
        BodyAsync<DealTransitionRequest>(context, cancellationToken,
            (request, token) => deals.TransitionAsync(ActorId(context), dealId, action, request, token));

    private static async Task<IResult> BodyAsync<T>(HttpContext context, CancellationToken cancellationToken,
        Func<T, CancellationToken, Task<DealResult>> action) where T : class
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            var (request, error) = await JsonRequestReader.ReadAsync<T>(context.Request, deadline.Token);
            return error ?? Map(await action(request!, deadline.Token));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Results.Problem(statusCode: 503,
                title: "The deal change is unconfirmed; reload the deal before retrying.");
        }
    }

    private static async Task<IResult> RunAsync(CancellationToken cancellationToken, string unavailable,
        Func<CancellationToken, Task<DealResult>> action)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            return Map(await action(deadline.Token));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Results.Problem(statusCode: 503, title: unavailable);
        }
    }

    private static Guid ActorId(HttpContext context) => Guid.Parse(context.User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);

    private static IResult Map(DealResult result) => result.Status switch
    {
        DealStatus.Succeeded => Results.Ok(result.Page ?? result.Deal ?? result.Vehicles ?? (object?)result.Options),
        DealStatus.Invalid => Results.Problem(statusCode: 400, title: "Provide valid deal details within the approved limits."),
        DealStatus.Forbidden => Results.Forbid(),
        DealStatus.NotFound => Results.NotFound(),
        DealStatus.Conflict => Results.Problem(statusCode: 409,
            title: "The deal, vehicle or customer changed or is unavailable; reload and retry."),
        DealStatus.Unavailable => Results.Problem(statusCode: 503, title: "Deal terms are temporarily unavailable."),
        _ => Results.Problem(statusCode: 500)
    };
}

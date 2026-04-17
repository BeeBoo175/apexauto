using System.IdentityModel.Tokens.Jwt;
using Dealership.Application.Vehicles;

namespace Dealership.Api.Endpoints;

public static class ListingCommandEndpoints
{
    public static void MapListingCommandEndpoints(this WebApplication app)
    {
        var routes = app.MapGroup("/api/staff/listings").RequireAuthorization();
        routes.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            return await next(context);
        });
        routes.MapPost("/{vehicleId:guid}/drafts", SaveAsync);
        routes.MapPost("/{vehicleId:guid}/publications", PublishAsync);
        routes.MapPost("/{vehicleId:guid}/withdrawals", WithdrawAsync);
    }

    private static Task<IResult> SaveAsync(Guid vehicleId, HttpContext context, ListingService listings,
        CancellationToken cancellationToken) => ExecuteAsync<ListingDraftRequest>(context,
            (actor, request, token) => listings.SaveAsync(actor, vehicleId, request, token), cancellationToken);

    private static Task<IResult> PublishAsync(Guid vehicleId, HttpContext context, ListingService listings,
        CancellationToken cancellationToken) => ExecuteAsync<ListingPublishRequest>(context,
            (actor, request, token) => listings.PublishAsync(actor, vehicleId, request, token), cancellationToken);

    private static Task<IResult> WithdrawAsync(Guid vehicleId, HttpContext context, ListingService listings,
        CancellationToken cancellationToken) => ExecuteAsync<ListingWithdrawRequest>(context,
            (actor, request, token) => listings.WithdrawAsync(actor, vehicleId, request, token), cancellationToken);

    private static async Task<IResult> ExecuteAsync<TRequest>(HttpContext context,
        Func<Guid, TRequest, CancellationToken, Task<ListingResult>> execute, CancellationToken cancellationToken)
        where TRequest : class
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            var (request, error) = await JsonRequestReader.ReadAsync<TRequest>(context.Request, deadline.Token, 32 * 1024);
            if (error is not null)
            {
                return error;
            }

            var actor = Guid.TryParse(context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id) ? id : Guid.Empty;
            var result = await execute(actor, request!, deadline.Token);
            return result.Status switch
            {
                ListingStatus.Succeeded => Results.Ok(result),
                ListingStatus.Invalid => Results.Problem(statusCode: 400, title: "Provide valid listing details and an operation ID."),
                ListingStatus.Forbidden => Results.Forbid(),
                ListingStatus.NotFound => Results.NotFound(),
                ListingStatus.Conflict => Results.Problem(statusCode: 409, title: "The listing, approval or inspection has changed."),
                _ => Results.Problem(statusCode: 500)
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Results.Problem(statusCode: 503, title: "Listing operation is unconfirmed; retry with the same operation and contents.");
        }
    }
}

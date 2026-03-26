using System.IdentityModel.Tokens.Jwt;
using Dealership.Application.Common;
using Dealership.Application.Vehicles;

namespace Dealership.Api.Endpoints;

public static class ListingReadEndpoints
{
    public static void MapListingReadEndpoints(this WebApplication app)
    {
        var routes = app.MapGroup("/api/staff/listings").RequireAuthorization();
        routes.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            return await next(context);
        });
        routes.MapGet("", ReadAsync);
        routes.MapGet("/{vehicleId:guid}", WorkspaceAsync);
        routes.MapGet("/{vehicleId:guid}/photos", PhotosAsync);
    }

    private static async Task<IResult> ReadAsync(Guid? branchId, int? page, int? pageSize, string? search,
        HttpContext context, IListingReadStore store, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            var result = await store.ReadAsync(ActorId(context),
                new(branchId, page ?? 1, pageSize ?? Pagination.DefaultPageSize, search?.Trim() ?? ""), deadline.Token);
            return Map(result.Status, result.Listings);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Unavailable();
        }
    }

    private static async Task<IResult> WorkspaceAsync(Guid vehicleId, HttpContext context,
        IListingReadStore store, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            var result = await store.WorkspaceAsync(ActorId(context), vehicleId, deadline.Token);
            return Map(result.Status, result.Workspace);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Unavailable();
        }
    }

    private static async Task<IResult> PhotosAsync(Guid vehicleId, int? page, int? pageSize, HttpContext context,
        IListingReadStore store, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            var result = await store.PhotosAsync(ActorId(context), vehicleId, page ?? 1,
                pageSize ?? Pagination.DefaultPageSize, deadline.Token);
            return Map(result.Status, result.Photos);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Unavailable();
        }
    }

    private static Guid ActorId(HttpContext context) => Guid.Parse(context.User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);

    private static IResult Unavailable() => Results.Problem(statusCode: 503, title: "Listing management is temporarily unavailable.");

    private static IResult Map(ListingStatus status, object? content) => status switch
    {
        ListingStatus.Succeeded => Results.Ok(content),
        ListingStatus.Invalid => Results.Problem(statusCode: 400, title: "Provide valid listing filters and a page size of 10, 25 or 50."),
        ListingStatus.Forbidden => Results.Forbid(),
        ListingStatus.NotFound => Results.NotFound(),
        ListingStatus.Unavailable => Unavailable(),
        _ => Results.Problem(statusCode: 500)
    };
}

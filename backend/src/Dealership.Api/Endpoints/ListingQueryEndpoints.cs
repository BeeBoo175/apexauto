using Dealership.Application.Common;
using Dealership.Application.Vehicles;
using static Dealership.Api.Endpoints.HttpContextActor;

namespace Dealership.Api.Endpoints;

public static class ListingQueryEndpoints
{
    public static void MapListingQueryEndpoints(this WebApplication app)
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
        routes.MapGet("/{vehicleId:guid}/history", HistoryAsync);
        routes.MapGet("/{vehicleId:guid}/revisions/{revisionId:guid}", RevisionAsync);
        routes.MapPost("/{vehicleId:guid}/price-estimates", EstimateAsync);
    }

    private static async Task<IResult> ReadAsync(Guid? branchId, int? page, int? pageSize, string? search,
        string? sort, string? direction, string? inspection, string? publication,
        decimal? minimumPrice, decimal? maximumPrice, decimal? minimumPublishedPrice, decimal? maximumPublishedPrice,
        string? from, string? to, string? registration, string? vehicle,
        HttpContext context, IListingReadStore store, CancellationToken cancellationToken)
    {
        var dates = ListDateRange.Parse(from, to);
        if (dates is null)
        {
            return Map(ListingStatus.Invalid, null);
        }
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            var result = await store.ReadAsync(ActorId(context),
                new(branchId, page ?? 1, pageSize ?? Pagination.DefaultPageSize, search?.Trim() ?? "",
                    sort ?? "", direction ?? "asc", inspection ?? "", publication ?? "",
                    minimumPrice, maximumPrice, minimumPublishedPrice, maximumPublishedPrice,
                    dates.From, dates.Until, registration?.Trim() ?? "", vehicle?.Trim() ?? ""), deadline.Token);
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

    private static async Task<IResult> HistoryAsync(Guid vehicleId, int? page, int? pageSize, HttpContext context,
        IListingHistoryStore history, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            var result = await history.ReadAsync(ActorId(context), vehicleId,
                new(page ?? 1, pageSize ?? Pagination.DefaultPageSize), deadline.Token);
            return Map(result.Status, result.History);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Unavailable();
        }
    }

    private static async Task<IResult> RevisionAsync(Guid vehicleId, Guid revisionId, HttpContext context,
        IListingHistoryStore history, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            var result = await history.RevisionAsync(ActorId(context), vehicleId, revisionId, deadline.Token);
            return Map(result.Status, result.Snapshot);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Unavailable();
        }
    }

    private static async Task<IResult> EstimateAsync(Guid vehicleId, HttpContext context,
        ListingEstimateService estimates, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            var (request, error) = await JsonRequestReader.ReadAsync<ListingEstimateRequest>(context.Request, deadline.Token, 4096);
            if (error is not null)
            {
                return error;
            }

            var result = await estimates.EstimateAsync(ActorId(context), vehicleId, request!, deadline.Token);
            if (result.Status == ListingStatus.Invalid)
            {
                return Results.Problem(statusCode: 400, title: "Provide a branch, positive vehicle price and nonnegative road tax in exact MYR minor units.");
            }

            return Map(result.Status, result.Estimate);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Unavailable();
        }
    }

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

using Dealership.Api.Configuration;
using Dealership.Application.Common;
using Dealership.Application.Sales;
using static Dealership.Api.Endpoints.HttpContextActor;

namespace Dealership.Api.Endpoints;

public static class SaleQueryEndpoints
{
    public static void MapSaleQueryEndpoints(this WebApplication app)
    {
        var routes = app.MapGroup("/api/staff/sales").RequireAuthorization();
        routes.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            return await next(context);
        });
        routes.MapGet("", TableAsync);
        routes.MapGet("/queue", QueueAsync);
        app.MapGet("/api/sales/{id:guid}/inspection-report", (Guid id, HttpContext context, ISaleDocumentStore documents,
            CancellationToken token) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            return ReadAsync(deadline => documents.InspectionReportAsync(ActorId(context), id, deadline), token);
        }).RequireAuthorization();
        app.MapGet("/api/sales/{id:guid}/documents/{documentId:guid}/pdf", (Guid id, Guid documentId,
            HttpContext context, SaleDocumentService documents, CancellationToken token) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            return ReadAsync(deadline => documents.ReadAsync(ActorId(context), id, documentId, deadline), token,
                content => Pdf(context, documentId, content));
        }).RequireAuthorization().RequireRateLimiting(ApiRateLimiting.PhotoTransferPolicy);
        routes.MapGet("/{id:guid}/settlement-review", (Guid id, HttpContext context, ISaleDocumentStore documents,
            CancellationToken token) => ReadAsync(deadline => documents.ReviewAsync(ActorId(context), id, deadline), token));
        app.MapGet("/api/sales/{id:guid}/documents", (Guid id, HttpContext context, ISaleDocumentStore documents,
            CancellationToken token) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            return ReadAsync(deadline => documents.ReadAsync(ActorId(context), id, deadline), token);
        }).RequireAuthorization();
        app.MapGet("/api/sales/{id:guid}/documents/{documentId:guid}", (Guid id, Guid documentId,
            HttpContext context, ISaleDocumentStore documents, CancellationToken token) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            return ReadAsync(deadline => documents.ReadOneAsync(ActorId(context), id, documentId, deadline), token);
        }).RequireAuthorization();
        routes.MapGet("/{id:guid}/vehicle", (Guid id, HttpContext context, ISaleReadStore sales,
            CancellationToken token) => ReadAsync(deadline => sales.VehicleAsync(ActorId(context), id, deadline), token));
        routes.MapGet("/{saleId:guid}/vehicle/photos/{id:guid}", (Guid saleId, Guid id, HttpContext context,
            SaleVehiclePhotoService photos, CancellationToken token) =>
                ListingPhotoEndpoints.DownloadAsync(context, (file, deadline) =>
                    photos.DownloadAsync(ActorId(context), saleId, id, file, deadline), token))
            .RequireRateLimiting(ApiRateLimiting.PhotoTransferPolicy);
        routes.MapGet("/{id:guid}", (Guid id, HttpContext context, ISaleReadStore sales, CancellationToken token) =>
            ReadAsync(deadline => sales.GetAsync(ActorId(context), id, deadline), token));
        routes.MapGet("/{id:guid}/options", (Guid id, HttpContext context, ISaleReadStore sales,
            CancellationToken token) => ReadAsync(deadline => sales.OptionsAsync(ActorId(context), id, deadline), token));
        routes.MapGet("/{id:guid}/agents", (Guid id, HttpContext context, ISaleReadStore sales,
            CancellationToken token) => ReadAsync(deadline => sales.AgentsAsync(ActorId(context), id, deadline), token));
        routes.MapGet("/{id:guid}/history", (Guid id, int? page, int? pageSize, HttpContext context,
            ISaleReadStore sales, CancellationToken token) =>
        {
            var (number, size) = (page ?? 1, pageSize ?? Pagination.DefaultPageSize);
            return Pagination.IsValid(number, size)
                ? ReadAsync(deadline => sales.HistoryAsync(ActorId(context), id, number, size, deadline), token)
                : Task.FromResult(Invalid());
        });
        routes.MapGet("/{id:guid}/versions/{version:long}", (Guid id, long version, HttpContext context,
            ISaleReadStore sales, CancellationToken token) => version < 1
            ? Task.FromResult(Invalid())
            : ReadAsync(deadline => sales.VersionAsync(ActorId(context), id, version, deadline), token));
    }

    private static Task<IResult> QueueAsync(string? view, string? search, int? page, int? pageSize,
        HttpContext context, ISaleListStore sales, CancellationToken cancellationToken)
    {
        var (number, size, text) = (page ?? 1, pageSize ?? Pagination.DefaultPageSize, search?.Trim());
        text = string.IsNullOrEmpty(text) ? null : text;
        return SaleQueries.ValidQueue(view, text, number, size)
            ? ReadAsync(deadline => sales.QueueAsync(ActorId(context), view ?? SaleViews.ToClaim, text,
                number, size, deadline), cancellationToken)
            : Task.FromResult(Invalid());
    }

    private static Task<IResult> TableAsync([AsParameters] SaleTableInput input,
        HttpContext context, ISaleListStore sales,
        CancellationToken cancellationToken)
    {
        var query = SaleQueries.Table(input);
        return query is null
            ? Task.FromResult(Invalid())
            : ReadAsync(deadline => sales.TableAsync(ActorId(context), query, deadline), cancellationToken);
    }

    private static async Task<IResult> ReadAsync<T>(Func<CancellationToken, Task<SaleReadResult<T>>> read,
        CancellationToken cancellationToken, Func<T, IResult>? success = null)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            var result = await read(deadline.Token);
            return result.Status switch
            {
                SaleResultStatus.Succeeded => success is null ? Results.Ok(result.Value) : success(result.Value!),
                SaleResultStatus.Invalid => Invalid(),
                SaleResultStatus.Forbidden => Results.Forbid(),
                SaleResultStatus.NotFound => Results.NotFound(),
                _ => Unavailable()
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Unavailable();
        }
    }

    private static IResult Pdf(HttpContext context, Guid documentId, byte[] content)
    {
        context.Response.Headers.XContentTypeOptions = "nosniff";
        context.Response.Headers.ContentDisposition = $"inline; filename=\"sale-document-{documentId:N}.pdf\"";
        return Results.Bytes(content, "application/pdf");
    }

    private static IResult Invalid() =>
        Results.Problem(statusCode: 400, title: $"Provide valid sale filters and a page size of {Pagination.PageSizeChoices}.");

    private static IResult Unavailable() =>
        Results.Problem(statusCode: 503, title: "Sales are temporarily unavailable.");
}

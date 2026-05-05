using System.IdentityModel.Tokens.Jwt;
using Dealership.Application.Common;
using Dealership.Application.Sales;

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
        routes.MapGet("/{id:guid}", (Guid id, HttpContext context, ISaleReadStore sales, CancellationToken token) =>
            ReadAsync(token, deadline => sales.GetAsync(ActorId(context), id, deadline)));
        routes.MapGet("/{id:guid}/options", (Guid id, HttpContext context, ISaleReadStore sales,
            CancellationToken token) => ReadAsync(token, deadline => sales.OptionsAsync(ActorId(context), id, deadline)));
        routes.MapGet("/{id:guid}/agents", (Guid id, HttpContext context, ISaleReadStore sales,
            CancellationToken token) => ReadAsync(token, deadline => sales.AgentsAsync(ActorId(context), id, deadline)));
        routes.MapGet("/{id:guid}/history", (Guid id, int? page, int? pageSize, HttpContext context,
            ISaleReadStore sales, CancellationToken token) =>
        {
            var (number, size) = (page ?? 1, pageSize ?? Pagination.DefaultPageSize);
            return Pagination.IsValid(number, size)
                ? ReadAsync(token, deadline => sales.HistoryAsync(ActorId(context), id, number, size, deadline))
                : Task.FromResult(Invalid());
        });
        routes.MapGet("/{id:guid}/versions/{version:long}", (Guid id, long version, HttpContext context,
            ISaleReadStore sales, CancellationToken token) => version < 1
            ? Task.FromResult(Invalid())
            : ReadAsync(token, deadline => sales.VersionAsync(ActorId(context), id, version, deadline)));
    }

    private static Task<IResult> QueueAsync(string? view, string? search, int? page, int? pageSize,
        HttpContext context, ISaleListStore sales, CancellationToken cancellationToken)
    {
        var (number, size, text) = (page ?? 1, pageSize ?? Pagination.DefaultPageSize, search?.Trim());
        text = string.IsNullOrEmpty(text) ? null : text;
        return SaleQueries.ValidQueue(view, text, number, size)
            ? ReadAsync(cancellationToken, deadline => sales.QueueAsync(ActorId(context), view ?? "ToClaim", text,
                number, size, deadline))
            : Task.FromResult(Invalid());
    }

    private static Task<IResult> TableAsync(int? page, int? pageSize, string? view, string? search, string? sort,
        string? direction, string? agent, string? branch, string? outcome, decimal? minimumDiscount,
        decimal? maximumDiscount, decimal? minimumBalance, decimal? maximumBalance, string? submittedFrom,
        string? submittedTo, string? closedFrom, string? closedTo, HttpContext context, ISaleListStore sales,
        CancellationToken cancellationToken)
    {
        var query = SaleQueries.Table(page ?? 1, pageSize ?? Pagination.DefaultPageSize, view, search, sort,
            direction, agent, branch, outcome, minimumDiscount, maximumDiscount, minimumBalance, maximumBalance,
            submittedFrom, submittedTo, closedFrom, closedTo);
        return query is null
            ? Task.FromResult(Invalid())
            : ReadAsync(cancellationToken, deadline => sales.TableAsync(ActorId(context), query, deadline));
    }

    private static async Task<IResult> ReadAsync<T>(CancellationToken cancellationToken,
        Func<CancellationToken, Task<SaleReadResult<T>>> read)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            var result = await read(deadline.Token);
            return result.Status switch
            {
                SaleStatus.Succeeded => Results.Ok(result.Value),
                SaleStatus.Invalid => Invalid(),
                SaleStatus.Forbidden => Results.Forbid(),
                SaleStatus.NotFound => Results.NotFound(),
                _ => Unavailable()
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Unavailable();
        }
    }

    private static IResult Invalid() =>
        Results.Problem(statusCode: 400, title: "Provide valid sale filters and a page size of 10, 25 or 50.");

    private static IResult Unavailable() =>
        Results.Problem(statusCode: 503, title: "Sales are temporarily unavailable.");

    private static Guid ActorId(HttpContext context) =>
        Guid.Parse(context.User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);
}

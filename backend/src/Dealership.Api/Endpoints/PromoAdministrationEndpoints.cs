using System.IdentityModel.Tokens.Jwt;
using Dealership.Application.Common;
using Dealership.Application.Vehicles;

namespace Dealership.Api.Endpoints;

public static class PromoAdministrationEndpoints
{
    public static void MapPromoAdministrationEndpoints(this WebApplication app)
    {
        var routes = app.MapGroup("/api/staff/promos").RequireAuthorization();
        routes.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            return await next(context);
        });
        routes.MapGet("", ListAsync);
        routes.MapPost("", CreateAsync);
        routes.MapPut("/{promoId:guid}", UpdateAsync);
    }

    private static async Task<IResult> ListAsync(int? page, int? pageSize, HttpContext context,
        PromoAdministrationService promos, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            var result = await promos.ListAsync(ActorId(context), page ?? 1, pageSize ?? Pagination.DefaultPageSize,
                deadline.Token);
            return Map(result);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Results.Problem(statusCode: 503, title: "Promos are temporarily unavailable.");
        }
    }

    private static async Task<IResult> CreateAsync(HttpContext context, PromoAdministrationService promos,
        CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            var (request, error) = await JsonRequestReader.ReadAsync<PromoCreateRequest>(context.Request, deadline.Token);
            return error ?? Map(await promos.CreateAsync(ActorId(context), request!, deadline.Token));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Unconfirmed();
        }
    }

    private static async Task<IResult> UpdateAsync(Guid promoId, HttpContext context, PromoAdministrationService promos,
        CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            var (request, error) = await JsonRequestReader.ReadAsync<PromoUpdateRequest>(context.Request, deadline.Token);
            return error ?? Map(await promos.UpdateAsync(ActorId(context), promoId, request!, deadline.Token));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Unconfirmed();
        }
    }

    private static Guid ActorId(HttpContext context) => Guid.Parse(context.User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);

    private static IResult Unconfirmed() => Results.Problem(statusCode: 503,
        title: "The promo change is unconfirmed; reload the promos before retrying.");

    private static IResult Map(PromoAdministrationResult result) => result.Status switch
    {
        PromoAdministrationStatus.Succeeded => Results.Ok((object?)result.Page ?? result.Promo),
        PromoAdministrationStatus.Invalid => Results.Problem(statusCode: 400, title: "Provide a valid promo within the approved limits."),
        PromoAdministrationStatus.Forbidden => Results.Forbid(),
        PromoAdministrationStatus.NotFound => Results.NotFound(),
        PromoAdministrationStatus.Conflict => Results.Problem(statusCode: 409,
            title: "The promo code or version conflicts with an existing promo; reload and retry."),
        _ => Results.Problem(statusCode: 500)
    };
}

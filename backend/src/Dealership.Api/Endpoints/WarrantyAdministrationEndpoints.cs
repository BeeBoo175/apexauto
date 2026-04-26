using System.IdentityModel.Tokens.Jwt;
using Dealership.Application.Common;
using Dealership.Application.Vehicles;

namespace Dealership.Api.Endpoints;

public static class WarrantyAdministrationEndpoints
{
    public static void MapWarrantyAdministrationEndpoints(this WebApplication app)
    {
        var routes = app.MapGroup("/api/staff/warranties").RequireAuthorization();
        routes.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            return await next(context);
        });
        routes.MapGet("", ListAsync);
        routes.MapGet("/{packageId:guid}", GetAsync);
        routes.MapPost("", CreateAsync);
        routes.MapPut("/{packageId:guid}", UpdateAsync);
    }

    private static async Task<IResult> ListAsync(int? page, int? pageSize, string? search, string? status, string? sort,
        string? direction, string? name, decimal? minimumPrice, decimal? maximumPrice, string? grade,
        HttpContext context, WarrantyAdministrationService warranties, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            var result = await warranties.ListAsync(ActorId(context), page ?? 1,
                pageSize ?? Pagination.DefaultPageSize, search, status, sort, direction, name, minimumPrice,
                maximumPrice, grade, deadline.Token);
            return Map(result);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Results.Problem(statusCode: 503, title: "Warranty packages are temporarily unavailable.");
        }
    }

    private static async Task<IResult> GetAsync(Guid packageId, HttpContext context,
        WarrantyAdministrationService warranties, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            return Map(await warranties.GetAsync(ActorId(context), packageId, deadline.Token));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Results.Problem(statusCode: 503, title: "The warranty package is temporarily unavailable.");
        }
    }

    private static async Task<IResult> CreateAsync(HttpContext context, WarrantyAdministrationService warranties,
        CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            var (request, error) = await JsonRequestReader.ReadAsync<WarrantyCreateRequest>(context.Request, deadline.Token);
            return error ?? Map(await warranties.CreateAsync(ActorId(context), request!, deadline.Token));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Unconfirmed();
        }
    }

    private static async Task<IResult> UpdateAsync(Guid packageId, HttpContext context,
        WarrantyAdministrationService warranties, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            var (request, error) = await JsonRequestReader.ReadAsync<WarrantyUpdateRequest>(context.Request, deadline.Token);
            return error ?? Map(await warranties.UpdateAsync(ActorId(context), packageId, request!, deadline.Token));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Unconfirmed();
        }
    }

    private static Guid ActorId(HttpContext context) => Guid.Parse(context.User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);

    private static IResult Unconfirmed() => Results.Problem(statusCode: 503,
        title: "The warranty change is unconfirmed; reload the packages before retrying.");

    private static IResult Map(WarrantyAdministrationResult result) => result.Status switch
    {
        WarrantyAdministrationStatus.Succeeded => Results.Ok((object?)result.Page ?? result.Package),
        WarrantyAdministrationStatus.Invalid => Results.Problem(statusCode: 400, title: "Provide a valid warranty package within the approved limits."),
        WarrantyAdministrationStatus.Forbidden => Results.Forbid(),
        WarrantyAdministrationStatus.NotFound => Results.NotFound(),
        WarrantyAdministrationStatus.Conflict => Results.Problem(statusCode: 409,
            title: "The package name or version conflicts with an existing package; reload and retry."),
        _ => Results.Problem(statusCode: 500)
    };
}

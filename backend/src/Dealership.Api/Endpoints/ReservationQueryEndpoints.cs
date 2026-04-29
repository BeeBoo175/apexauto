using System.IdentityModel.Tokens.Jwt;
using Dealership.Application.Common;
using Dealership.Application.Vehicles;

namespace Dealership.Api.Endpoints;

public static class ReservationQueryEndpoints
{
    public static void MapReservationQueryEndpoints(this WebApplication app)
    {
        var routes = app.MapGroup("/api").RequireAuthorization();
        routes.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            return await next(context);
        });
        routes.MapGet("/account/reservations", AccountAsync);
        routes.MapGet("/staff/reservations", StaffAsync);
        routes.MapGet("/staff/reservations/{reservationId:guid}", DetailAsync);
    }

    private static async Task<IResult> AccountAsync(int? page, HttpContext context, IReservationReadStore store,
        CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            var result = await store.AccountAsync(ActorId(context), page ?? 1, deadline.Token);
            return Map(result);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Unavailable();
        }
    }

    private static async Task<IResult> StaffAsync(Guid? branchId, int? page, int? pageSize, bool? isActive,
        string? sort, string? direction, string? search, string? registration, string? vehicle, string? buyer, string? status,
        decimal? minimumTotal, decimal? maximumTotal, string? createdFrom, string? createdTo,
        string? expiresFrom, string? expiresTo, string? releasedFrom, string? releasedTo, bool? claimed, string? agent, HttpContext context,
        IReservationReadStore store, CancellationToken cancellationToken)
    {
        var created = ListDateRange.Parse(createdFrom, createdTo);
        var expires = ListDateRange.Parse(expiresFrom, expiresTo);
        var released = ListDateRange.Parse(releasedFrom, releasedTo);
        if (created is null || expires is null || released is null)
        {
            return Map(new ReservationHistoryResult(ReservationStatus.Invalid));
        }
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            var result = await store.StaffAsync(ActorId(context),
                new(branchId, page ?? 1, pageSize ?? Pagination.DefaultPageSize, isActive,
                    sort ?? "", direction ?? "asc", search?.Trim() ?? "", registration?.Trim() ?? "",
                    vehicle?.Trim() ?? "", buyer?.Trim() ?? "", status ?? "", minimumTotal, maximumTotal,
                    created.From, created.Until, expires.From, expires.Until, released.From, released.Until, claimed, agent?.Trim() ?? ""), deadline.Token);
            return Map(result);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Unavailable();
        }
    }

    private static Guid ActorId(HttpContext context) => Guid.Parse(context.User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);

    private static async Task<IResult> DetailAsync(Guid reservationId, HttpContext context,
        IReservationReadStore store, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            var result = await store.DetailAsync(ActorId(context), reservationId, deadline.Token);
            return result.Status switch
            {
                ReservationStatus.Succeeded => Results.Ok(result),
                ReservationStatus.Invalid => Results.BadRequest(),
                ReservationStatus.Forbidden => Results.Forbid(),
                ReservationStatus.NotFound => Results.NotFound(),
                ReservationStatus.Unavailable => Unavailable(),
                _ => Results.Problem(statusCode: 500)
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Unavailable();
        }
    }

    private static IResult Unavailable() => Results.Problem(statusCode: 503, title: "Reservation history is temporarily unavailable.");

    private static IResult Map(ReservationHistoryResult result) => result.Status switch
    {
        ReservationStatus.Succeeded => Results.Ok(result.History),
        ReservationStatus.Invalid => Results.Problem(statusCode: 400, title: "Provide valid reservation filters and a staff page size of 10, 25 or 50."),
        ReservationStatus.Forbidden => Results.Forbid(),
        ReservationStatus.Unavailable => Unavailable(),
        _ => Results.Problem(statusCode: 500)
    };
}

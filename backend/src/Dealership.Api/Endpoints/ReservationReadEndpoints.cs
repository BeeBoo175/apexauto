using System.IdentityModel.Tokens.Jwt;
using Dealership.Application.Common;
using Dealership.Application.Vehicles;

namespace Dealership.Api.Endpoints;

public static class ReservationReadEndpoints
{
    public static void MapReservationReadEndpoints(this WebApplication app)
    {
        var routes = app.MapGroup("/api").RequireAuthorization();
        routes.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            return await next(context);
        });
        routes.MapGet("/account/reservations", AccountAsync);
        routes.MapGet("/staff/reservations", StaffAsync);
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

    private static async Task<IResult> StaffAsync(Guid? branchId, int? page, int? pageSize, HttpContext context,
        IReservationReadStore store, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            var result = await store.StaffAsync(ActorId(context),
                new(branchId, page ?? 1, pageSize ?? Pagination.DefaultPageSize), deadline.Token);
            return Map(result);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Unavailable();
        }
    }

    private static Guid ActorId(HttpContext context) => Guid.Parse(context.User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);

    private static IResult Unavailable() => Results.Problem(statusCode: 503, title: "Reservation history is temporarily unavailable.");

    private static IResult Map(ReservationHistoryResult result) => result.Status switch
    {
        ReservationStatus.Succeeded => Results.Ok(result.History),
        ReservationStatus.Invalid => Results.Problem(statusCode: 400, title: "Provide a valid reservation page and staff page size of 10, 25 or 50."),
        ReservationStatus.Forbidden => Results.Forbid(),
        ReservationStatus.Unavailable => Unavailable(),
        _ => Results.Problem(statusCode: 500)
    };
}

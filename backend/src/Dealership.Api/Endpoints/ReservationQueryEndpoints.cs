using Dealership.Api.Configuration;
using Dealership.Application.Common;
using Dealership.Application.Vehicles;
using static Dealership.Api.Endpoints.HttpContextActor;

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
    }

    private static async Task<IResult> AccountAsync(int? page, string? from, string? to, string? search, string? view,
        HttpContext context, IReservationReadStore store, CancellationToken cancellationToken)
    {
        if (view is not null && view != "collected")
        {
            return Results.Problem(statusCode: 400, title: "Provide a supported reservation view.");
        }

        var dates = ListDateRange.Parse(from, to);
        if (dates is null || !ListSearch.IsValid(search ?? ""))
        {
            return Results.Problem(statusCode: 400, title: "Provide a valid paired date range of at most 366 days and a short search.");
        }

        return await BoundedCommand.RunAsync(async deadline =>
        {
            var result = await store.AccountAsync(ActorId(context), page ?? 1, deadline, dates, search,
                view == "collected");
            return Map(result);
        }, Unavailable, cancellationToken);
    }

    private static IResult Unavailable() => Results.Problem(statusCode: 503, title: "Reservation history is temporarily unavailable.");

    private static IResult Map(ReservationHistoryResult result) => result.Status switch
    {
        ReservationStatus.Succeeded => Results.Ok(result.History),
        ReservationStatus.Invalid => Results.Problem(statusCode: 400, title: "Provide a valid reservation page."),
        ReservationStatus.Forbidden => Results.Forbid(),
        ReservationStatus.Unavailable => Unavailable(),
        _ => Results.Problem(statusCode: 500)
    };
}

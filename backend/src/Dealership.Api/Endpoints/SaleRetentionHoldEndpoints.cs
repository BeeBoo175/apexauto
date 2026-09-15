using System.Text.Json.Serialization;
using Dealership.Application.Sales;
using static Dealership.Api.Endpoints.HttpContextActor;

namespace Dealership.Api.Endpoints;

public static class SaleRetentionHoldEndpoints
{
    public static void MapSaleRetentionHoldEndpoints(this WebApplication app)
    {
        var routes = app.MapGroup("/api/staff/retention-holds").RequireAuthorization();
        routes.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            return await next(context);
        });
        routes.MapGet("", ListAsync);
        routes.MapPut("", SetAsync);
    }

    private static async Task<IResult> ListAsync(HttpContext context, SaleRetentionHoldService holds,
        CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            var result = await holds.ListAsync(ActorId(context), deadline.Token);
            return result.Status == SaleRetentionHoldStatus.Succeeded ? Results.Ok(result.Holds) : Results.Forbid();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Results.Problem(statusCode: 503, title: "Preservation holds are temporarily unavailable.");
        }
    }

    private static async Task<IResult> SetAsync(HttpContext context, SaleRetentionHoldService holds,
        CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            var (input, error) = await JsonRequestReader.ReadAsync<HoldInput>(context.Request, deadline.Token);
            if (error is not null)
            {
                return error;
            }

            var result = await holds.SetByNumberAsync(ActorId(context),
                new(input!.ReservationNumber, input.Until, input.Reason), deadline.Token);
            return result.Status switch
            {
                SaleRetentionHoldStatus.Succeeded => Results.Ok(result.Hold),
                SaleRetentionHoldStatus.Invalid => Results.Problem(statusCode: 400,
                    title: "Provide a reservation number, a future date within ten years and a reason, or no date to release."),
                SaleRetentionHoldStatus.Forbidden => Results.Forbid(),
                _ => Results.NotFound()
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Results.Problem(statusCode: 503,
                title: "The hold change is unconfirmed; reload the holds before repeating it.");
        }
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed record HoldInput(string? ReservationNumber, DateTimeOffset? Until, string? Reason);
}

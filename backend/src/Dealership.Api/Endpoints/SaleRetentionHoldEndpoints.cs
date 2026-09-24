using System.Text.Json.Serialization;
using Dealership.Api.Configuration;
using Dealership.Application.Sales;
using static Dealership.Api.Endpoints.HttpContextActor;

namespace Dealership.Api.Endpoints;

public static class SaleRetentionHoldEndpoints
{
    public static void MapSaleRetentionHoldEndpoints(this WebApplication app)
    {
        var routes = app.MapGroup("/api/staff/retention-holds").RequireAuthorization().NoStore();
        routes.MapGet("", ListAsync);
        routes.MapPut("", SetAsync);
    }

    private static async Task<IResult> ListAsync(HttpContext context, SaleRetentionHoldService holds,
        CancellationToken cancellationToken)
    {
        return await BoundedRead.RunAsync(async token =>
        {
            var result = await holds.ListAsync(ActorId(context), token);
            return result.Status == SaleRetentionHoldStatus.Succeeded
                ? Results.Ok(new { holds = result.Holds, total = result.Total })
                : Results.Forbid();
        }, "Preservation holds are temporarily unavailable.", cancellationToken);
    }

    private static async Task<IResult> SetAsync(HttpContext context, SaleRetentionHoldService holds,
        CancellationToken cancellationToken)
    {
        return await BoundedCommand.RunAsync(async deadline =>
        {
            var (input, error) = await JsonRequestReader.ReadAsync<HoldInput>(context.Request, deadline);
            if (error is not null)
            {
                return error;
            }

            var result = await holds.SetByNumberAsync(ActorId(context),
                new(input!.ReservationNumber, input.Until, input.Reason), deadline);
            return result.Status switch
            {
                SaleRetentionHoldStatus.Succeeded => Results.Ok(result.Hold),
                SaleRetentionHoldStatus.Invalid => Results.Problem(statusCode: 400,
                    title: "Provide a reservation number, a future date within ten years and a reason, or no date to release."),
                SaleRetentionHoldStatus.Forbidden => Results.Forbid(),
                _ => Results.NotFound()
            };
        }, () => Results.Problem(statusCode: 503,
            title: "The hold change is unconfirmed; reload the holds before repeating it."), cancellationToken);
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed record HoldInput(string? ReservationNumber, DateTimeOffset? Until, string? Reason);
}

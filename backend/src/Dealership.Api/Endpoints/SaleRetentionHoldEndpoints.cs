using System.Text.Json.Serialization;
using Dealership.Application.Sales;
using static Dealership.Api.Endpoints.HttpContextActor;

namespace Dealership.Api.Endpoints;

public static class SaleRetentionHoldEndpoints
{
    public static void MapSaleRetentionHoldEndpoints(this WebApplication app)
    {
        app.MapPut("/api/staff/sales/{id:guid}/retention-hold", SetAsync).RequireAuthorization();
    }

    private static async Task<IResult> SetAsync(Guid id, HttpContext context, SaleRetentionHoldService holds,
        CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            var (input, error) = await JsonRequestReader.ReadAsync<HoldInput>(context.Request, deadline.Token);
            if (error is not null)
            {
                return error;
            }

            var result = await holds.SetAsync(ActorId(context), id, new(input!.Until, input.Reason), deadline.Token);
            return result.Status switch
            {
                SaleRetentionHoldStatus.Succeeded => Results.Ok(result.Hold),
                SaleRetentionHoldStatus.Invalid => Results.Problem(statusCode: 400,
                    title: "Provide a future date within ten years and a reason, or no date to release the hold."),
                SaleRetentionHoldStatus.Forbidden => Results.Forbid(),
                _ => Results.NotFound()
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Results.Problem(statusCode: 503,
                title: "The hold change is unconfirmed; read the sale before repeating it.");
        }
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed record HoldInput(DateTimeOffset? Until, string? Reason);
}

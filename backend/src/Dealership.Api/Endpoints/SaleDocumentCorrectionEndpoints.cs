using System.IdentityModel.Tokens.Jwt;
using System.Text.Json.Serialization;
using Dealership.Application.Sales;

namespace Dealership.Api.Endpoints;

public static class SaleDocumentCorrectionEndpoints
{
    public static void MapSaleDocumentCorrectionEndpoints(this WebApplication app)
    {
        app.MapPost("/api/staff/sales/{id:guid}/document-corrections", ReissueAsync).RequireAuthorization();
    }

    private static async Task<IResult> ReissueAsync(Guid id, HttpContext context,
        SaleDocumentCorrectionService corrections, CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            var (input, error) = await JsonRequestReader.ReadAsync<CorrectionInput>(context.Request, deadline.Token);
            if (error is not null)
            {
                return error;
            }

            var actor = Guid.Parse(context.User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);
            var request = new SaleDocumentCorrectionRequest(input!.OperationId, input.ExpectedVersion, input.DocumentId,
                new(input.BuyerName!, input.BuyerAddress!, input.Reason!));
            var result = await corrections.ReissueAsync(actor, id, request, deadline.Token);
            return result.Status switch
            {
                SaleResultStatus.Succeeded => Results.Ok(result.Receipt),
                SaleResultStatus.Invalid => Results.Problem(statusCode: 400, title: "Provide buyer details and a correction reason."),
                SaleResultStatus.Forbidden => Results.Forbid(),
                SaleResultStatus.NotFound => Results.NotFound(),
                SaleResultStatus.Conflict => Results.Problem(statusCode: 409,
                    title: "The sale or document changed, or this operation was already used for different contents."),
                _ => Results.Problem(statusCode: 503, title: "Document correction is temporarily unavailable.")
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Results.Problem(statusCode: 503,
                title: "The document correction is unconfirmed; reconcile with the same operation and contents.");
        }
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed record CorrectionInput(Guid OperationId, long ExpectedVersion, Guid DocumentId,
        string? BuyerName, string? BuyerAddress, string? Reason);
}

using Dealership.Application.Vehicles;
using Dealership.Domain.Vehicles;

namespace Dealership.Api.Endpoints;

public static class PromoEndpoints
{
    public static void MapPromoEndpoints(this WebApplication app)
    {
        app.MapPost("/api/promos/validate", ValidateAsync);
    }

    private static async Task<IResult> ValidateAsync(PurchaseQuoteService quotes,
        HttpContext context, CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";
        var (request, error) = await JsonRequestReader.ReadAsync<PromoValidationRequest>(context.Request, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        if (request is null)
        {
            return Results.Problem(statusCode: 400);
        }

        if (!PromoCode.ValidCode(request.Code))
        {
            return Results.Problem(statusCode: 400, title: "Provide one valid promo code.");
        }

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            var result = await quotes.QuoteAsync(request.VehicleId, request.Code, deadline.Token);
            return result.Status switch
            {
                PurchaseQuoteStatus.Succeeded => Results.Ok(result.Quote),
                PurchaseQuoteStatus.Invalid => Results.Problem(statusCode: 400, title: "Provide a valid vehicle and promo code."),
                PurchaseQuoteStatus.PromoUnavailable => Results.Problem(statusCode: 400, title: "This promo cannot apply to the vehicle."),
                PurchaseQuoteStatus.NotFound => Results.NotFound(),
                _ => Unavailable()
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Unavailable();
        }
    }

    private static IResult Unavailable() => Results.Problem(statusCode: 503, title: "Pricing is temporarily unavailable.");
}

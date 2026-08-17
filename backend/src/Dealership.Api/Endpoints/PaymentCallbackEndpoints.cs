using Dealership.Api.Configuration;
using Dealership.Application.Vehicles;
using Dealership.Infrastructure.Vehicles;

namespace Dealership.Api.Endpoints;

public static class PaymentCallbackEndpoints
{
    public static void MapPaymentCallbackEndpoints(this WebApplication app)
    {
        // The provider calls this without a session; the signed fields are the only authority, so the route stays
        // under the ordinary address budget and answers 200 once a verified delivery is recorded.
        app.MapPost(BillplzPaymentGateway.CallbackPath, ReceiveAsync).NoStore();
    }

    private static async Task<IResult> ReceiveAsync(HttpContext context, ReservationPaymentService payments,
        CancellationToken cancellationToken)
    {
        if (!context.Request.HasFormContentType)
        {
            return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);
        }

        var form = await context.Request.ReadFormAsync(cancellationToken);
        var fields = form.ToDictionary(field => field.Key, field => field.Value.ToString(), StringComparer.Ordinal);
        return await payments.CompleteAsync(fields, cancellationToken)
            ? Results.Ok()
            : Results.StatusCode(StatusCodes.Status400BadRequest);
    }
}

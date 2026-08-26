using Dealership.Api.Configuration;
using Dealership.Application.Vehicles;

namespace Dealership.Api.Endpoints;

public static class PaymentCallbackEndpoints
{
    public static void MapPaymentCallbackEndpoints(this WebApplication app)
    {
        // The provider calls this without a session; the signed fields are the only authority, so the route stays
        // under the ordinary address budget and answers 200 once a verified delivery is recorded.
        app.MapPost(PaymentRoutes.Callback, ReceiveAsync).NoStore();
        // Whether deposits go through the payment provider, so the pages can word the deposit as real or simulated.
        app.MapGet(PaymentRoutes.Mode, (ReservationPaymentService payments) =>
            Results.Ok(new PaymentModeReceipt(payments.IsEnabled))).NoStore();
        // The customer's browser brings the same signed fields back from the payment page, so the page confirms at once.
        app.MapPost(PaymentRoutes.Return, ReceiveReturnAsync)
            .RequireRateLimiting(ApiRateLimiting.VerificationPolicy).NoStore();
    }

    internal static string StatusName(DepositPaymentStatus status) => status switch
    {
        DepositPaymentStatus.Confirmed => "confirmed",
        DepositPaymentStatus.Waiting => "waiting",
        DepositPaymentStatus.PaymentRequired => "payment-required",
        DepositPaymentStatus.Lapsed => "lapsed",
        _ => "conflict"
    };

    private static async Task<IResult> ReceiveAsync(HttpContext context, ReservationPaymentService payments,
        CancellationToken cancellationToken)
    {
        return await ProviderCallback.ReadFormAsync(context, payments.CompleteAsync, cancellationToken);
    }

    private static async Task<IResult> ReceiveReturnAsync(HttpContext context, ReservationPaymentService payments,
        CancellationToken cancellationToken)
    {
        var (request, error) = await JsonRequestReader.ReadAsync<PaymentReturnRequest>(context.Request, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        if (request?.Fields is not { Count: > 0 and <= 16 } fields ||
            fields.Any(field => field.Key.Length > 64 || field.Value is null || field.Value.Length > 128))
        {
            return Results.Problem(statusCode: 400, title: "The payment return is invalid.");
        }

        var result = await payments.CompleteReturnAsync(fields, cancellationToken);
        return result.Status == DepositPaymentStatus.Invalid
            ? Results.Problem(statusCode: 400, title: "The payment return is invalid.")
            : Results.Ok(new ReservationCommandEndpoints.DepositPaymentStatusReceipt(StatusName(result.Status)));
    }

    private sealed record PaymentModeReceipt(bool Live);

    private sealed record PaymentReturnRequest(Dictionary<string, string>? Fields);
}

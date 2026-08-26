using Dealership.Api.Configuration;
using Dealership.Application.Vehicles;
using static Dealership.Api.Endpoints.HttpContextActor;

namespace Dealership.Api.Endpoints;

public static class ReservationRefundEndpoints
{
    public static void MapReservationRefundEndpoints(this WebApplication app)
    {
        var routes = app.MapGroup("/api/reservation-refunds").RequireAuthorization().NoStore();
        routes.MapGet("/", ListAsync);
        routes.MapPost("/{paymentRequestId:guid}/bank-account", RequestAsync);
        // The provider confirms a payout without a session; its checksum is the only authority.
        app.MapPost(PaymentRoutes.PayoutCallback, ReceiveCallbackAsync).NoStore();
    }

    private static async Task<IResult> ListAsync(HttpContext context, ReservationRefundService refunds,
        CancellationToken cancellationToken)
    {
        var list = await refunds.ListAsync(ActorId(context), cancellationToken);
        var banks = PayoutBanks.All.Select(bank => new BankReceipt(bank.Code, bank.Name)).ToList();
        return Results.Ok(new RefundListReceipt(refunds.IsEnabled, banks, list));
    }

    private static async Task<IResult> RequestAsync(Guid paymentRequestId, HttpContext context,
        ReservationRefundService refunds, CancellationToken cancellationToken)
    {
        var (account, error) = await JsonRequestReader.ReadAsync<BankAccountDetails>(context.Request, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        var result = await refunds.RequestAsync(ActorId(context), paymentRequestId, account!, cancellationToken);
        return result.Status switch
        {
            RefundStatus.Processing or RefundStatus.Completed => Results.Ok(result.Refund),
            RefundStatus.Invalid => Results.Problem(statusCode: 400,
                title: "Provide a bank, a numeric account number and the account holder."),
            RefundStatus.NotFound => Results.NotFound(),
            _ => Results.Problem(statusCode: 503, title: "The refund service is temporarily unavailable.")
        };
    }

    private static async Task<IResult> ReceiveCallbackAsync(HttpContext context, ReservationRefundService refunds,
        CancellationToken cancellationToken)
    {
        return await ProviderCallback.ReadFormAsync(context, refunds.CompleteAsync, cancellationToken);
    }

    private sealed record BankReceipt(string Code, string Name);

    private sealed record RefundListReceipt(bool Enabled, IReadOnlyList<BankReceipt> Banks, IReadOnlyList<RefundView> Refunds);
}

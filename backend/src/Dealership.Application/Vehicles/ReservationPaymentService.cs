using Dealership.Domain.Vehicles;

namespace Dealership.Application.Vehicles;

// The hosted deposit for a staff deposit request. The link starts one bill per request, the gateway's signed
// callback is the only thing that confirms the reservation, and the return page only reads the stored state.
public sealed class ReservationPaymentService(IReservationDepositRequestStore requests,
    IReservationPaymentStore payments, IPaymentGateway gateway, ReservationDepositRequestService deposits,
    TimeProvider clock)
{
    public async Task<DepositPaymentResult> StartAsync(string? token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 160)
        {
            return new(DepositPaymentStatus.Invalid);
        }

        var request = await requests.FindByTokenAsync(token, cancellationToken);
        if (request is null)
        {
            return new(DepositPaymentStatus.Invalid);
        }

        if (request.ConfirmedAt is null && request.IsPending(clock.GetUtcNow()) && !gateway.IsEnabled)
        {
            return Map(await deposits.ConfirmAsync(token, cancellationToken));
        }

        return await StartForRequestAsync(request, cancellationToken);
    }

    public bool IsEnabled => gateway.IsEnabled;

    // Opens or replays the bill of a request that is still holding its car; used by the emailed link and by a
    // customer paying for their own reservation.
    public async Task<DepositPaymentResult> StartForRequestAsync(ReservationDepositRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ConfirmedAt is not null)
        {
            return new(DepositPaymentStatus.Confirmed);
        }

        if (!request.IsPending(clock.GetUtcNow()))
        {
            return new(DepositPaymentStatus.Lapsed);
        }

        var existing = await payments.FindByRequestAsync(request.Id, cancellationToken);
        if (existing is not null)
        {
            return Describe(existing);
        }

        try
        {
            return await CreatePaymentAsync(request, cancellationToken);
        }
        catch (PaymentGatewayException)
        {
            return new(DepositPaymentStatus.Unavailable);
        }
    }

    // What the return page shows: stored state only, never a call to the gateway.
    public async Task<DepositPaymentResult> StatusAsync(Guid requestId, CancellationToken cancellationToken)
    {
        var request = requestId == Guid.Empty ? null : await requests.FindAsync(requestId, cancellationToken);
        if (request is null)
        {
            return new(DepositPaymentStatus.Invalid);
        }

        if (request.ConfirmedAt is not null)
        {
            return new(DepositPaymentStatus.Confirmed);
        }

        var payment = await payments.FindByRequestAsync(requestId, cancellationToken);
        if (payment is { State: DepositPaymentState.PaidUnmatched })
        {
            return new(DepositPaymentStatus.Conflict);
        }

        if (!request.IsPending(clock.GetUtcNow()))
        {
            return new(DepositPaymentStatus.Lapsed);
        }

        return new(payment is null ? DepositPaymentStatus.PaymentRequired : DepositPaymentStatus.Waiting);
    }

    // False only when the signature is invalid; every verified delivery is acknowledged so the gateway stops retrying.
    public async Task<bool> CompleteAsync(IReadOnlyDictionary<string, string> fields, CancellationToken cancellationToken)
    {
        var notice = gateway.IsEnabled ? gateway.ReadNotice(fields) : null;
        if (notice is null)
        {
            return false;
        }

        if (notice.Paid)
        {
            await RecordPaymentAsync(notice.BillId, notice.PaidAmountSen, notice.PaidAt, cancellationToken);
        }

        return true;
    }

    // The browser returns from the payment page with the provider's signed fields; a valid signature proves the bill
    // was paid in full as well as the callback does, so the customer need not wait for the callback to arrive.
    public async Task<DepositPaymentResult> CompleteReturnAsync(IReadOnlyDictionary<string, string> fields,
        CancellationToken cancellationToken)
    {
        var notice = gateway.IsEnabled ? gateway.ReadNotice(fields) : null;
        var payment = notice is null ? null : await payments.FindByBillAsync(notice.BillId, cancellationToken);
        if (notice is null || payment is null)
        {
            return new(DepositPaymentStatus.Invalid);
        }

        if (notice.Paid)
        {
            await RecordPaymentAsync(notice.BillId, notice.PaidAmountSen, notice.PaidAt, cancellationToken);
        }

        return await StatusAsync(payment.RequestId, cancellationToken);
    }

    private async Task RecordPaymentAsync(string billId, int? paidAmountSen, DateTimeOffset? paidAt,
        CancellationToken cancellationToken)
    {
        var payment = await payments.FindByBillAsync(billId, cancellationToken);
        if (payment is null || payment.State != DepositPaymentState.Pending)
        {
            return;
        }

        var paidAmount = paidAmountSen ?? payment.AmountSen;
        var reserved = false;
        if (paidAmount == payment.AmountSen)
        {
            var confirmation = await deposits.ConfirmPaidAsync(payment.RequestId, paidAt, cancellationToken);
            reserved = confirmation.Status == ReservationStatus.Succeeded;
        }

        await payments.RecordPaidAsync(billId, paidAmount, reserved, clock.GetUtcNow(), cancellationToken);
    }

    private async Task<DepositPaymentResult> CreatePaymentAsync(ReservationDepositRequest request,
        CancellationToken cancellationToken)
    {
        var payer = await payments.FindPayerAsync(request.CustomerId, cancellationToken);
        if (payer is null)
        {
            return new(DepositPaymentStatus.Invalid);
        }

        var amountSen = (int)(Reservation.DepositAmount * 100m);
        var bill = await gateway.CreateBillAsync(new(request.Id, payer.Name, payer.Email, amountSen,
            "ApexAuto reservation deposit"), cancellationToken);
        var payment = new ReservationDepositPayment(request.Id, bill.Id, bill.Url, amountSen, clock.GetUtcNow());
        if (await payments.AddAsync(payment, cancellationToken))
        {
            return Describe(payment);
        }

        // A parallel start won the race; its bill stays and this one is removed so it cannot be paid as well.
        await gateway.DeleteBillAsync(bill.Id, cancellationToken);
        var winner = await payments.FindByRequestAsync(request.Id, cancellationToken);
        return winner is null ? new(DepositPaymentStatus.Unavailable) : Describe(winner);
    }

    private static DepositPaymentResult Describe(ReservationDepositPayment payment) => payment.State switch
    {
        DepositPaymentState.Paid => new(DepositPaymentStatus.Confirmed),
        DepositPaymentState.PaidUnmatched => new(DepositPaymentStatus.Conflict),
        _ => new(DepositPaymentStatus.PaymentRequired, payment.BillUrl)
    };

    private static DepositPaymentResult Map(ReservationResult result) => result.Status switch
    {
        ReservationStatus.Succeeded => new(DepositPaymentStatus.Confirmed),
        ReservationStatus.Invalid or ReservationStatus.NotFound => new(DepositPaymentStatus.Invalid),
        ReservationStatus.DepositRequestLapsed => new(DepositPaymentStatus.Lapsed),
        _ => new(DepositPaymentStatus.Conflict)
    };
}

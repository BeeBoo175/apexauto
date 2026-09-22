using Dealership.Application.Accounts;
using Dealership.Domain.Vehicles;

namespace Dealership.Application.Vehicles;

// The hosted deposit for a staff deposit request. The link starts one bill per request, the gateway's signed
// callback is the only thing that confirms the reservation, and the return page only reads the stored state.
public sealed class ReservationPaymentService(IReservationDepositRequestStore requests,
    IReservationPaymentStore payments, IPaymentGateway gateway, ReservationDepositRequestService deposits,
    TimeProvider clock)
{
    private static readonly TimeSpan LapsedBillWindow = TimeSpan.FromHours(2);

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
            await ReleaseBillAsync(request.Id, cancellationToken);
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
            await ReleaseBillAsync(request.Id, cancellationToken);
            return new(DepositPaymentStatus.Lapsed);
        }

        return new(payment is null ? DepositPaymentStatus.PaymentRequired : DepositPaymentStatus.Waiting);
    }

    // Staff cancel a deposit request; a bill that turns out to be paid keeps the reservation instead.
    public async Task<ReservationStatus> CancelAsync(Guid requestId, AssistingStaff staff,
        CancellationToken cancellationToken)
    {
        var access = await deposits.CanCancelAsync(requestId, staff, cancellationToken);
        if (access != ReservationStatus.Succeeded)
        {
            return access;
        }

        return await CancelRequestAsync(requestId, () => deposits.CancelAsync(requestId, staff, cancellationToken),
            cancellationToken);
    }

    // A customer cancels a payment they opened; a bill that turns out to be paid keeps the reservation instead.
    public async Task<ReservationStatus> CancelOwnAsync(Guid customerId, Guid requestId,
        CancellationToken cancellationToken)
    {
        var access = await deposits.CanCancelOwnAsync(customerId, requestId, cancellationToken);
        if (access != ReservationStatus.Succeeded)
        {
            return access;
        }

        return await CancelRequestAsync(requestId, () => deposits.CancelOwnAsync(customerId, requestId, cancellationToken),
            cancellationToken);
    }

    // The bill is settled before the request is cancelled: a cancelled request can no longer be confirmed, so a payment
    // that is only found afterwards would be refunded while the car is already released.
    private async Task<ReservationStatus> CancelRequestAsync(Guid requestId, Func<Task<ReservationStatus>> cancel,
        CancellationToken cancellationToken)
    {
        if (await KeepPaidDepositAsync(requestId, cancellationToken))
        {
            return ReservationStatus.DepositAlreadyPaid;
        }

        var cancelled = await cancel();
        if (cancelled == ReservationStatus.Succeeded)
        {
            await ReleaseBillAsync(requestId, cancellationToken);
        }

        return cancelled;
    }

    // True when the deposit was paid, so the request is confirmed into its reservation. An unpaid bill is deleted here;
    // when the provider cannot delete it, it is asked whether the bill was paid, and an unreachable provider leaves the
    // cancellation to go ahead with the bill released later.
    private async Task<bool> KeepPaidDepositAsync(Guid requestId, CancellationToken cancellationToken)
    {
        var payment = await payments.FindByRequestAsync(requestId, cancellationToken);
        if (payment is { State: DepositPaymentState.Paid })
        {
            return true;
        }

        if (payment is not { State: DepositPaymentState.Pending, BillDeletedAt: null })
        {
            return false;
        }

        if (await gateway.DeleteBillAsync(payment.BillId, cancellationToken))
        {
            await payments.MarkBillDeletedAsync(requestId, clock.GetUtcNow(), cancellationToken);
            return false;
        }

        var notice = await gateway.FindBillAsync(payment.BillId, cancellationToken);
        if (notice is not { Paid: true })
        {
            return false;
        }

        await RecordPaymentAsync(payment.BillId, notice.PaidAmountSen, notice.PaidAt, cancellationToken);
        var settled = await payments.FindByRequestAsync(requestId, cancellationToken);
        return settled is { State: DepositPaymentState.Paid };
    }

    // A cancelled or lapsed request no longer holds the car, so its unpaid bill is removed instead of staying payable.
    // The provider refuses to delete a bill that was already paid, which leaves a late payment to the unmatched refund.
    public async Task ReleaseBillAsync(Guid requestId, CancellationToken cancellationToken)
    {
        var payment = await payments.FindByRequestAsync(requestId, cancellationToken);
        if (payment is { State: DepositPaymentState.Pending, BillDeletedAt: null } &&
            await gateway.DeleteBillAsync(payment.BillId, cancellationToken))
        {
            await payments.MarkBillDeletedAsync(requestId, clock.GetUtcNow(), cancellationToken);
        }
    }

    // Removes the bills of requests that stopped holding their car without anyone reading them again. Only recent
    // requests are looked at, so the sweep stays short; removing a bill that is already gone does nothing.
    public async Task ReleaseLapsedBillsAsync(CancellationToken cancellationToken)
    {
        if (!gateway.IsEnabled)
        {
            return;
        }

        var now = clock.GetUtcNow();
        foreach (var requestId in await payments.ReleasableRequestsAsync(now - LapsedBillWindow, now, 50,
            cancellationToken))
        {
            await ReleaseBillAsync(requestId, cancellationToken);
        }
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
        var added = await payments.AddAsync(payment, cancellationToken);
        if (added == DepositPaymentAddResult.Added)
        {
            return Describe(payment);
        }

        // The bill was created outside the transaction, so a lost race or a request that stopped holding its car
        // removes it here so it cannot be paid.
        await gateway.DeleteBillAsync(bill.Id, cancellationToken);
        if (added == DepositPaymentAddResult.Refused)
        {
            return new(DepositPaymentStatus.Lapsed);
        }

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

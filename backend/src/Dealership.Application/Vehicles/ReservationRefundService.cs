using Dealership.Domain.Vehicles;

namespace Dealership.Application.Vehicles;

// Pays a customer's deposit back through the provider once it is refundable. The customer gives the bank account the
// money goes to, because the provider keeps no payer bank details; the account number goes straight to the provider
// and is never stored, and the provider's signed callback settles the payout.
public sealed class ReservationRefundService(IReservationRefundStore refunds, IPayoutGateway gateway, TimeProvider clock)
{
    public bool IsEnabled => gateway.IsEnabled;

    public async Task<IReadOnlyList<RefundView>> ListAsync(Guid customerId, CancellationToken cancellationToken)
    {
        if (customerId == Guid.Empty)
        {
            return [];
        }

        await ReconcileAsync(customerId, cancellationToken);
        return await refunds.ListAsync(customerId, cancellationToken);
    }

    // A payout whose callback never arrived, such as while the API is not reachable by the provider, settles when its
    // customer next looks, from the provider's own record of it.
    private async Task ReconcileAsync(Guid customerId, CancellationToken cancellationToken)
    {
        if (!gateway.IsEnabled)
        {
            return;
        }

        foreach (var payoutId in await refunds.ProcessingPayoutsAsync(customerId, cancellationToken))
        {
            try
            {
                var outcome = await gateway.GetPayoutOutcomeAsync(payoutId, cancellationToken);
                await SettleAsync(payoutId, null, outcome, cancellationToken);
            }
            catch (PaymentGatewayException)
            {
                // The refund stays processing and is checked again at the next look.
            }
        }
    }

    private async Task SettleAsync(string payoutId, string? reference, PayoutOutcome outcome,
        CancellationToken cancellationToken)
    {
        var refund = await refunds.FindByPayoutAsync(payoutId, cancellationToken) ??
            await FindUnattachedAsync(reference, cancellationToken);
        if (refund is null)
        {
            return;
        }

        if (refund.PayoutId is null)
        {
            refund.AttachPayout(payoutId);
        }

        var settled = outcome switch
        {
            PayoutOutcome.Completed => refund.Settle(true, clock.GetUtcNow()),
            PayoutOutcome.Failed => refund.Settle(false, clock.GetUtcNow()),
            _ => false
        };
        if (settled)
        {
            await refunds.SaveAsync(refund, cancellationToken);
        }
    }

    // The creation of this attempt's payout timed out, so no payout id was saved; the provider's notice names the
    // reference it was created with.
    private async Task<ReservationRefund?> FindUnattachedAsync(string? reference, CancellationToken cancellationToken)
    {
        if (!ReservationRefund.TryParseReference(reference, out var paymentRequestId, out var attempt))
        {
            return null;
        }

        var refund = await refunds.FindAsync(paymentRequestId, cancellationToken);
        return refund is { PayoutId: null, State: RefundState.Processing } && refund.Attempt == attempt ? refund : null;
    }

    public async Task<RefundResult> RequestAsync(Guid customerId, Guid paymentRequestId, BankAccountDetails account,
        CancellationToken cancellationToken)
    {
        if (customerId == Guid.Empty || paymentRequestId == Guid.Empty || !IsValid(account))
        {
            return new(RefundStatus.Invalid);
        }

        if (!gateway.IsEnabled)
        {
            return new(RefundStatus.Unavailable);
        }

        var due = await refunds.FindDueAsync(customerId, paymentRequestId, cancellationToken);
        if (due is null)
        {
            return new(RefundStatus.NotFound);
        }

        var now = clock.GetUtcNow();
        var refund = await refunds.FindAsync(paymentRequestId, cancellationToken);
        if (refund is null)
        {
            refund = new ReservationRefund(paymentRequestId, customerId, due.AmountSen, account.BankCode!,
                account.AccountNumber!, account.AccountHolder!, now);
            if (!await refunds.AddAsync(refund, cancellationToken))
            {
                // A parallel request holds the refund; repeating a request never pays twice.
                refund = await refunds.FindAsync(paymentRequestId, cancellationToken);
                return refund is null ? new(RefundStatus.Unavailable) : Describe(refund, due);
            }
        }
        else
        {
            var previousAttempt = refund.Attempt;
            if (!refund.Retry(account.BankCode!, account.AccountNumber!, account.AccountHolder!, now))
            {
                return Describe(refund, due);
            }

            if (!await refunds.SaveRetryAsync(refund, previousAttempt, cancellationToken))
            {
                // A parallel retry won; only its request asks the provider for a payout.
                refund = await refunds.FindAsync(paymentRequestId, cancellationToken);
                return refund is null ? new(RefundStatus.Unavailable) : Describe(refund, due);
            }
        }

        return await PayOutAsync(refund, due, account.AccountNumber!, cancellationToken);
    }

    // False only when the checksum is invalid; every verified delivery is acknowledged so the provider stops retrying.
    public async Task<bool> CompleteAsync(IReadOnlyDictionary<string, string> fields, CancellationToken cancellationToken)
    {
        var notice = gateway.IsEnabled ? gateway.ReadNotice(fields) : null;
        if (notice is null)
        {
            return false;
        }

        await SettleAsync(notice.PayoutId, notice.Reference, notice.Outcome, cancellationToken);
        return true;
    }

    private async Task<RefundResult> PayOutAsync(ReservationRefund refund, RefundDue due, string accountNumber,
        CancellationToken cancellationToken)
    {
        // A refund still being paid out always has its holder; only a completed refund loses it to retention.
        if (refund.AccountHolder is not { } accountHolder)
        {
            throw new InvalidOperationException("A refund without an account holder cannot be paid out.");
        }

        try
        {
            var payout = await gateway.CreatePayoutAsync(new(refund.Reference, refund.BankCode, accountNumber,
                accountHolder, refund.AmountSen, "ApexAuto reservation deposit refund"), cancellationToken);
            refund.AttachPayout(payout.Id);
            if (payout.Outcome == PayoutOutcome.Completed)
            {
                refund.Settle(true, clock.GetUtcNow());
            }

            await refunds.SaveAsync(refund, cancellationToken);
            return Describe(refund, due);
        }
        catch (PaymentGatewayTimeoutException)
        {
            // The payout may already have been made. The refund stays processing and never starts a new attempt; the
            // provider's notice names this attempt's reference and settles it.
            return Describe(refund, due);
        }
        catch (PaymentGatewayException)
        {
            // The provider did not take it, so the customer can ask again instead of waiting for a callback.
            refund.Fail(clock.GetUtcNow());
            await refunds.SaveAsync(refund, cancellationToken);
            return new(RefundStatus.Unavailable);
        }
    }

    private static RefundResult Describe(ReservationRefund refund, RefundDue due)
    {
        var view = new RefundView(refund.PaymentRequestId, refund.AmountSen, due.Reason, string.Empty,
            RefundView.StateName(refund.State), refund.BankCode, refund.AccountLast4, refund.RequestedAt);
        return new(refund.State == RefundState.Completed ? RefundStatus.Completed : RefundStatus.Processing, view);
    }

    private static bool IsValid(BankAccountDetails account) =>
        !string.IsNullOrWhiteSpace(account.BankCode) && PayoutBanks.IsSupported(account.BankCode) &&
        ReservationRefund.IsValidAccountNumber(account.AccountNumber) &&
        ReservationRefund.IsValidAccountHolder(account.AccountHolder);
}

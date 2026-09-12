using Dealership.Application.Vehicles;
using Dealership.Domain.Accounts;

namespace Dealership.Application.Accounts;

// An Administrator erases a customer who cannot or will not do it themselves. Blockers come before the password so a
// blocked account is no password oracle, the slow hash runs before any transaction, and provider bills are deleted
// outside the locked command, exactly as in the customer's own closure.
public sealed class AccountErasureService(IAccountErasureStore store, ReservationPaymentService payments)
{
    public Task<bool> IsAdministratorAsync(Guid administratorId, CancellationToken cancellationToken) =>
        administratorId == Guid.Empty ? Task.FromResult(false) : store.IsAdministratorAsync(administratorId, cancellationToken);

    public Task<AccountErasurePreview?> PreviewCustomerAsync(Guid administratorId, Guid customerId,
        CancellationToken cancellationToken) =>
        administratorId == Guid.Empty || customerId == Guid.Empty
            ? Task.FromResult<AccountErasurePreview?>(null)
            : store.PreviewAsync(administratorId, customerId, staff: false, cancellationToken);

    public async Task<AccountErasureResult> EraseCustomerAsync(Guid administratorId, Guid customerId,
        AccountErasureRequest request, CancellationToken cancellationToken)
    {
        var reason = request.Reason?.Trim();
        if (administratorId == Guid.Empty || customerId == Guid.Empty || request.OperationId == Guid.Empty ||
            string.IsNullOrEmpty(request.Password) || !AccountEvent.IsValidReason(reason))
        {
            return new(AccountErasureStatus.Invalid);
        }

        if (!await store.IsAdministratorAsync(administratorId, cancellationToken))
        {
            return new(AccountErasureStatus.Forbidden);
        }

        // A repeated command returns its receipt even though the account is no longer open.
        var applied = await store.FindReceiptAsync(administratorId, customerId, request.OperationId, cancellationToken);
        if (applied is not null)
        {
            return applied;
        }

        var preview = await store.PreviewAsync(administratorId, customerId, staff: false, cancellationToken);
        if (preview is null)
        {
            return new(AccountErasureStatus.NotFound);
        }

        if (preview.CustomerBlockers.Any(item => item.Type != ClosureBlockerTypes.DepositBill))
        {
            return new(AccountErasureStatus.Blocked, preview.CustomerBlockers);
        }

        var password = await store.VerifyAdministratorPasswordAsync(administratorId, request.Password, cancellationToken);
        if (password.Outcome != ClosurePasswordOutcome.Verified)
        {
            return new(password.Outcome == ClosurePasswordOutcome.WrongPassword
                ? AccountErasureStatus.WrongPassword
                : AccountErasureStatus.Forbidden);
        }

        foreach (var requestId in await store.UnreleasedBillRequestsAsync(customerId, cancellationToken))
        {
            await payments.ReleaseBillAsync(requestId, cancellationToken);
        }

        return await store.EraseCustomerAsync(administratorId, customerId, request with { Reason = reason },
            password.SecurityStamp!, cancellationToken);
    }
}

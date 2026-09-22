using Dealership.Application.Vehicles;

namespace Dealership.Application.Accounts;

// A customer closes their own account. The order protects the customer and the data: blockers are read before the
// password so a blocked account is no password oracle, the slow password hash runs before any transaction, and only
// then are provider bills deleted and the locked command run.
public sealed class CustomerClosureService(ICustomerClosureStore store, ReservationPaymentService payments,
    IBusinessEvidencePeriod evidencePeriod)
{
    public async Task<IReadOnlyList<RetainedCategory>> RetainedAsync(CancellationToken cancellationToken) =>
        ClosureRetention.Categories(await evidencePeriod.YearsAsync(cancellationToken));

    public Task<IReadOnlyList<ClosureBlocker>?> BlockersAsync(Guid customerId, CancellationToken cancellationToken) =>
        customerId == Guid.Empty ? Task.FromResult<IReadOnlyList<ClosureBlocker>?>(null)
            : store.BlockersAsync(customerId, cancellationToken);

    public async Task<CustomerClosureResult> CloseAsync(Guid customerId, CustomerClosureRequest request,
        CancellationToken cancellationToken)
    {
        if (customerId == Guid.Empty || request.OperationId == Guid.Empty || string.IsNullOrEmpty(request.Password))
        {
            return new(CustomerClosureStatus.Invalid);
        }

        var blockers = await store.BlockersAsync(customerId, cancellationToken);
        if (blockers is null)
        {
            return new(CustomerClosureStatus.Forbidden);
        }

        if (ClosureBills.HasBlockingObligation(blockers))
        {
            return new(CustomerClosureStatus.Blocked, blockers);
        }

        var password = await store.VerifyPasswordAsync(customerId, request.Password, cancellationToken);
        if (password.Outcome != ClosurePasswordOutcome.Verified)
        {
            return new(password.Outcome switch
            {
                ClosurePasswordOutcome.WrongPassword => CustomerClosureStatus.WrongPassword,
                ClosurePasswordOutcome.ProtectedSample => CustomerClosureStatus.ProtectedSample,
                _ => CustomerClosureStatus.Forbidden
            });
        }

        await ClosureBills.ReleaseAsync(store, payments, customerId, cancellationToken);

        return await store.CloseAsync(customerId, request.OperationId, password.SecurityStamp!, cancellationToken);
    }
}

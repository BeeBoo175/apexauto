using Dealership.Application.Vehicles;

namespace Dealership.Application.Accounts;

// Steps both account-closing services run around a customer's deposit bills.
internal static class ClosureBills
{
    // A deposit bill is released by the service itself, so every other blocker still stops the closure.
    public static bool HasBlockingObligation(IReadOnlyList<ClosureBlocker> blockers) =>
        blockers.Any(item => item.Type != ClosureBlockerTypes.DepositBill);

    // The provider call stays outside the transaction; the locked command blocks if any bill survived.
    public static async Task ReleaseAsync(IClosureBillStore store, ReservationPaymentService payments,
        Guid customerId, CancellationToken cancellationToken)
    {
        foreach (var requestId in await store.UnreleasedBillRequestsAsync(customerId, cancellationToken))
        {
            await payments.ReleaseBillAsync(requestId, cancellationToken);
        }
    }
}

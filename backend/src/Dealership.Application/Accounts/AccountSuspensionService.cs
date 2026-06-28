using Dealership.Domain.Accounts;

namespace Dealership.Application.Accounts;

public sealed class AccountSuspensionService(IAccountSuspensionStore store)
{
    public Task<AccountSuspensionStatus> SuspendAsync(Guid administratorId, Guid targetId,
        AccountSuspensionRequest request, CancellationToken cancellationToken)
    {
        var reason = request.Reason?.Trim();
        if (administratorId == Guid.Empty || targetId == Guid.Empty || request.OperationId == Guid.Empty ||
            string.IsNullOrEmpty(reason) || reason.Length > AccountEvent.ReasonMaximumLength ||
            reason.Any(char.IsControl))
        {
            return Task.FromResult(AccountSuspensionStatus.Invalid);
        }

        return store.SuspendAsync(administratorId, targetId, request with { Reason = reason }, cancellationToken);
    }

    // Lifting records no reason; the event kind carries it.
    public Task<AccountSuspensionStatus> LiftAsync(Guid administratorId, Guid targetId,
        AccountSuspensionRequest request, CancellationToken cancellationToken)
    {
        if (administratorId == Guid.Empty || targetId == Guid.Empty || request.OperationId == Guid.Empty)
        {
            return Task.FromResult(AccountSuspensionStatus.Invalid);
        }

        return store.LiftAsync(administratorId, targetId, request with { Reason = null }, cancellationToken);
    }
}

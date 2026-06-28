namespace Dealership.Application.Accounts;

// Suspension cuts access at once and keeps the account and its work. The two security choices are independent
// and off by default: a reset replaces the password so the person must recover it, and verification asks them to
// confirm their email again.
public sealed record AccountSuspensionRequest(Guid OperationId, string? Reason, bool RequirePasswordReset,
    bool RequireEmailVerification);

public enum AccountSuspensionStatus
{
    Succeeded,
    Invalid,
    Forbidden,
    NotFound,
    // An administrator cannot suspend themselves, the last active Administrator cannot be suspended, and only an
    // active account can be suspended or a suspended one lifted.
    Conflict
}

public interface IAccountSuspensionStore
{
    Task<AccountSuspensionStatus> SuspendAsync(Guid administratorId, Guid targetId, AccountSuspensionRequest request,
        CancellationToken cancellationToken);

    Task<AccountSuspensionStatus> LiftAsync(Guid administratorId, Guid targetId, AccountSuspensionRequest request,
        CancellationToken cancellationToken);
}

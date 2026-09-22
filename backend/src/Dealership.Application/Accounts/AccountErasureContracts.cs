namespace Dealership.Application.Accounts;

public enum AccountErasureStatus
{
    Erased,
    Invalid,
    // The caller is not an active Administrator.
    Forbidden,
    // No open account of the requested kind.
    NotFound,
    // The Administrator's own password was wrong; answered 409 so the browser stays signed in.
    WrongPassword,
    // A customer record or an employee's open work still needs resolving; the result lists it.
    Blocked,
    // A published sample account, the Administrator's own account, or the last active Administrator.
    Refused,
    // The operation id already recorded a different action on this account.
    Conflict,
    // The Administrator's credentials changed between the password check and the lock.
    SessionChanged,
    // The database rolled the attempt back for a lock conflict; nothing was changed.
    RetryLater
}

// The Administrator's final confirmation of an erasure: a reason for the audit trail and their own password.
public sealed record AccountErasureRequest(Guid OperationId, string? Reason, string? Password);

public sealed record AccountErasureResult(AccountErasureStatus Status,
    IReadOnlyList<ClosureBlocker>? CustomerBlockers = null, IReadOnlyList<AccountBlocker>? StaffBlockers = null,
    CustomerClosureReceipt? Receipt = null);

// What an Administrator sees before confirming: blockers of the target and the categories that survive erasure.
public sealed record AccountErasurePreview(IReadOnlyList<ClosureBlocker> CustomerBlockers,
    IReadOnlyList<AccountBlocker> StaffBlockers);

public interface IAccountErasureStore : IClosureBillStore
{
    Task<bool> IsAdministratorAsync(Guid administratorId, CancellationToken cancellationToken);

    // The receipt of an erasure this Administrator already applied to this account, or null.
    Task<AccountErasureResult?> FindReceiptAsync(Guid administratorId, Guid targetId, Guid operationId,
        CancellationToken cancellationToken);

    // Null when the caller is not an Administrator or the target is not an open account of the requested kind.
    Task<AccountErasurePreview?> PreviewAsync(Guid administratorId, Guid targetId, bool staff,
        CancellationToken cancellationToken);

    Task<ClosurePasswordCheck> VerifyAdministratorPasswordAsync(Guid administratorId, string password,
        CancellationToken cancellationToken);

    Task<AccountErasureResult> EraseStaffAsync(Guid administratorId, Guid staffId, AccountErasureRequest request,
        string verifiedSecurityStamp, CancellationToken cancellationToken);

    Task<AccountErasureResult> EraseCustomerAsync(Guid administratorId, Guid customerId, AccountErasureRequest request,
        string verifiedSecurityStamp, CancellationToken cancellationToken);
}

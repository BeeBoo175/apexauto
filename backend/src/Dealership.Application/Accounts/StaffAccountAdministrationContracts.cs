namespace Dealership.Application.Accounts;

// Work an employee still owns. An Administrator sees what kind of record it is, its vehicle registration, state,
// branch and the managers who resolve it, with no customer detail and no link, because administrators have no
// operational access.
public sealed record AccountBlocker(string Type, string Reference, string Status, string BranchName, string Managers);

public enum AccountAdministrationStatus
{
    Succeeded,
    Invalid,
    Forbidden,
    NotFound,
    // Self, last active Administrator, a wrong current state or an unusable branch.
    Conflict,
    // The employee still owns open work; the response lists it.
    Blocked
}

public sealed record AccountAdministrationResult(AccountAdministrationStatus Status,
    IReadOnlyList<AccountBlocker>? Blockers = null);

public sealed record AccountDeactivationRequest(Guid OperationId, string? Reason);

public sealed record AccountTransferRequest(Guid OperationId, string? Reason, Guid? BranchId);

public sealed record AccountReactivationRequest(Guid OperationId);

public interface IStaffAccountAdministrationStore
{
    Task<AccountAdministrationResult> BlockersAsync(Guid administratorId, Guid staffId, CancellationToken cancellationToken);

    Task<AccountAdministrationResult> DeactivateAsync(Guid administratorId, Guid staffId,
        AccountDeactivationRequest request, CancellationToken cancellationToken);

    Task<AccountAdministrationResult> ReactivateAsync(Guid administratorId, Guid staffId,
        AccountReactivationRequest request, CancellationToken cancellationToken);

    Task<AccountAdministrationResult> TransferAsync(Guid administratorId, Guid staffId,
        AccountTransferRequest request, CancellationToken cancellationToken);
}

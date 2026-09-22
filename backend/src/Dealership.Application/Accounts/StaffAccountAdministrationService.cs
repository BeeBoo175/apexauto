using Dealership.Domain.Accounts;

namespace Dealership.Application.Accounts;

public sealed class StaffAccountAdministrationService(IStaffAccountAdministrationStore store,
    IBusinessEvidencePeriod evidencePeriod)
{
    public async Task<IReadOnlyList<RetainedCategory>> RetainedAsync(CancellationToken cancellationToken) =>
        ClosureRetention.StaffCategories(await evidencePeriod.YearsAsync(cancellationToken));

    public Task<AccountAdministrationResult> BlockersAsync(Guid administratorId, Guid staffId,
        CancellationToken cancellationToken) =>
        administratorId == Guid.Empty || staffId == Guid.Empty
            ? Task.FromResult(new AccountAdministrationResult(AccountAdministrationStatus.Invalid))
            : store.BlockersAsync(administratorId, staffId, cancellationToken);

    public Task<AccountAdministrationResult> DeactivateAsync(Guid administratorId, Guid staffId,
        AccountDeactivationRequest request, CancellationToken cancellationToken)
    {
        var reason = request.Reason?.Trim();
        return Invalid(administratorId, staffId, request.OperationId) || !AccountEvent.IsValidReason(reason)
            ? Task.FromResult(new AccountAdministrationResult(AccountAdministrationStatus.Invalid))
            : store.DeactivateAsync(administratorId, staffId, request with { Reason = reason }, cancellationToken);
    }

    public Task<AccountAdministrationResult> ReactivateAsync(Guid administratorId, Guid staffId,
        AccountReactivationRequest request, CancellationToken cancellationToken) =>
        Invalid(administratorId, staffId, request.OperationId)
            ? Task.FromResult(new AccountAdministrationResult(AccountAdministrationStatus.Invalid))
            : store.ReactivateAsync(administratorId, staffId, request, cancellationToken);

    public Task<AccountAdministrationResult> TransferAsync(Guid administratorId, Guid staffId,
        AccountTransferRequest request, CancellationToken cancellationToken)
    {
        var reason = request.Reason?.Trim();
        return Invalid(administratorId, staffId, request.OperationId) || !AccountEvent.IsValidReason(reason) ||
            request.BranchId is null || request.BranchId == Guid.Empty
            ? Task.FromResult(new AccountAdministrationResult(AccountAdministrationStatus.Invalid))
            : store.TransferAsync(administratorId, staffId, request with { Reason = reason }, cancellationToken);
    }

    private static bool Invalid(Guid administratorId, Guid staffId, Guid operationId) =>
        administratorId == Guid.Empty || staffId == Guid.Empty || operationId == Guid.Empty;

}

using System.Data;
using Dealership.Application.Accounts;
using Dealership.Domain.Accounts;
using Dealership.Domain.Common;
using Dealership.Infrastructure.Identity;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Accounts;

// Deactivation, reactivation and branch transfer of staff accounts. Each locks the Administrator rows and the
// employee, rechecks the open work inside the same transaction and writes its event with the change.
public sealed class StaffAccountAdministrationStore(DealershipDbContext database, TimeProvider clock)
    : IStaffAccountAdministrationStore
{
    private static readonly AccountAdministrationResult SucceededResult = new(AccountAdministrationStatus.Succeeded);

    public async Task<AccountAdministrationResult> BlockersAsync(Guid administratorId, Guid staffId,
        CancellationToken cancellationToken)
    {
        if (!await AdministratorScope.IsAdministratorAsync(database, administratorId, cancellationToken))
        {
            return new(AccountAdministrationStatus.Forbidden);
        }

        var staff = await database.Users.AsNoTracking().Where(user => user.Id == staffId)
            .Select(user => new { user.Role }).SingleOrDefaultAsync(cancellationToken);
        if (staff is null || staff.Role == AccountRoles.Customer)
        {
            return new(AccountAdministrationStatus.NotFound);
        }

        return new(AccountAdministrationStatus.Succeeded,
            await StaffWorkBlockers.ReadAsync(database, staffId, Now(), cancellationToken));
    }

    public Task<AccountAdministrationResult> DeactivateAsync(Guid administratorId, Guid staffId,
        AccountDeactivationRequest request, CancellationToken cancellationToken) =>
        ChangeAsync(administratorId, staffId, request.OperationId, async (user, administrators, now) =>
        {
            if (!user.IsActive && user.SuspendedAt is null)
            {
                return Conflict();
            }

            if (user.Id == administratorId || (user.Role == AccountRoles.Administrator && user.IsActive &&
                !administrators.Any(item => item.Id != user.Id)))
            {
                return Conflict();
            }

            var blockers = await StaffWorkBlockers.ReadAsync(database, user.Id, now, cancellationToken);
            if (blockers.Count > 0)
            {
                return new AccountAdministrationResult(AccountAdministrationStatus.Blocked, blockers);
            }

            var before = user.SuspendedAt is null ? "Active" : "Suspended";
            user.IsActive = false;
            user.SuspendedAt = null;
            user.SecurityStamp = Guid.NewGuid().ToString();
            await AccountAccessRevocation.RevokeAsync(database, user.Id, now, cancellationToken);
            database.AccountEvents.Add(new AccountEvent(request.OperationId, user.Id, administratorId,
                AccountEventKind.Deactivated, request.Reason, [new("Access", before, "Deactivated")], now));
            return SucceededResult;
        }, cancellationToken);

    public Task<AccountAdministrationResult> ReactivateAsync(Guid administratorId, Guid staffId,
        AccountReactivationRequest request, CancellationToken cancellationToken) =>
        ChangeAsync(administratorId, staffId, request.OperationId, async (user, _, now) =>
        {
            if (user.IsActive || user.SuspendedAt is not null)
            {
                return Conflict();
            }

            // A branch-scoped employee comes back only to an active branch.
            if (user.BranchId is { } branchId && !await database.Branches.AnyAsync(
                    branch => branch.Id == branchId && branch.IsActive, cancellationToken))
            {
                return Conflict();
            }

            user.IsActive = true;
            user.SecurityStamp = Guid.NewGuid().ToString();
            database.AccountEvents.Add(new AccountEvent(request.OperationId, user.Id, administratorId,
                AccountEventKind.Reactivated, null, [new("Access", "Deactivated", "Active")], now));
            return SucceededResult;
        }, cancellationToken);

    public Task<AccountAdministrationResult> TransferAsync(Guid administratorId, Guid staffId,
        AccountTransferRequest request, CancellationToken cancellationToken) =>
        ChangeAsync(administratorId, staffId, request.OperationId, async (user, _, now) =>
        {
            if (!user.IsActive || user.BranchId is null || user.BranchId == request.BranchId)
            {
                return Conflict();
            }

            var branches = await database.Branches.AsNoTracking()
                .Where(branch => branch.Id == user.BranchId || branch.Id == request.BranchId)
                .ToDictionaryAsync(branch => branch.Id, cancellationToken);
            if (!branches.TryGetValue(request.BranchId!.Value, out var target) || !target.IsActive)
            {
                return Conflict();
            }

            var blockers = await StaffWorkBlockers.ReadAsync(database, user.Id, now, cancellationToken);
            if (blockers.Count > 0)
            {
                return new AccountAdministrationResult(AccountAdministrationStatus.Blocked, blockers);
            }

            var before = branches.GetValueOrDefault(user.BranchId.Value)?.Name;
            user.BranchId = target.Id;
            // Current access changes at once; sessions stay, and past work keeps its original branch.
            database.AccountEvents.Add(new AccountEvent(request.OperationId, user.Id, administratorId,
                AccountEventKind.BranchTransferred, request.Reason, [new("Branch", before, target.Name)], now));
            return SucceededResult;
        }, cancellationToken);

    private static AccountAdministrationResult Conflict() => new(AccountAdministrationStatus.Conflict);

    private async Task<AccountAdministrationResult> ChangeAsync(Guid administratorId, Guid staffId, Guid operationId,
        Func<AccountUser, List<AccountUser>, DateTimeOffset, Task<AccountAdministrationResult>> apply,
        CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted,
            cancellationToken);
        var locked = await AdministratorScope.LockTargetAsync(database, administratorId, staffId, operationId,
            lockAdministrators: true, cancellationToken);
        switch (locked.Outcome)
        {
            case AdministratorTargetOutcome.Forbidden:
                return new(AccountAdministrationStatus.Forbidden);
            case AdministratorTargetOutcome.Replayed:
                return SucceededResult;
            case AdministratorTargetOutcome.NotFound:
                return new(AccountAdministrationStatus.NotFound);
        }

        var user = locked.User!;
        if (user.Role == AccountRoles.Customer)
        {
            return new(AccountAdministrationStatus.NotFound);
        }

        var result = await apply(user, locked.Administrators!, Now());
        if (result.Status != AccountAdministrationStatus.Succeeded)
        {
            return result;
        }

        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    private DateTimeOffset Now() => UtcTime.TruncateToMicroseconds(clock.GetUtcNow());
}

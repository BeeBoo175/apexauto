using Dealership.Application.Accounts;
using Dealership.Domain.Accounts;
using Dealership.Domain.Common;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Development;
using Dealership.Infrastructure.Identity;
using Dealership.Infrastructure.Persistence;
using Dealership.Infrastructure.Photos;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Accounts;

// An Administrator erasing another account. The lock order matches a customer's own closure, with the actor's row
// share-locked before the target so a deactivated or demoted Administrator cannot finish an erasure.
public sealed class AccountErasureStore(DealershipDbContext database, IPasswordHasher<AccountUser> passwords,
    MissingAccountPasswordHash missingHash, PhotoCleanupSignal cleanup, IBusinessEvidencePeriod evidencePeriod)
    : IAccountErasureStore
{
    public Task<bool> IsAdministratorAsync(Guid administratorId, CancellationToken cancellationToken) =>
        AdministratorScope.IsAdministratorAsync(database, administratorId, cancellationToken);

    public async Task<AccountErasureResult?> FindReceiptAsync(Guid administratorId, Guid targetId, Guid operationId,
        CancellationToken cancellationToken)
    {
        var receipt = await database.AccountEvents.AsNoTracking().SingleOrDefaultAsync(item => item.Id == operationId &&
            item.TargetId == targetId && item.Kind == AccountEventKind.Erased && item.ActorId == administratorId,
            cancellationToken);
        if (receipt is null)
        {
            return null;
        }

        var staff = await database.Users.AsNoTracking().AnyAsync(user => user.Id == targetId &&
            user.Role != AccountRoles.Customer, cancellationToken);
        return await ErasedAsync(targetId, receipt.Id, receipt.RecordedAt, staff, cancellationToken);
    }

    public async Task<AccountErasurePreview?> PreviewAsync(Guid administratorId, Guid targetId, bool staff,
        CancellationToken cancellationToken)
    {
        if (!await AdministratorScope.IsAdministratorAsync(database, administratorId, cancellationToken))
        {
            return null;
        }

        var open = await database.Users.AsNoTracking().AnyAsync(user => user.Id == targetId && user.ClosedAt == null &&
            (staff ? user.Role != AccountRoles.Customer : user.Role == AccountRoles.Customer), cancellationToken);
        if (!open)
        {
            return null;
        }

        var now = await database.DatabaseNowAsync(cancellationToken);
        return staff
            ? new([], await StaffWorkBlockers.ReadAsync(database, targetId, now, cancellationToken))
            : new(await CustomerClosureBlockers.ReadAsync(database, targetId, now, cancellationToken), []);
    }

    public async Task<ClosurePasswordCheck> VerifyAdministratorPasswordAsync(Guid administratorId, string password,
        CancellationToken cancellationToken)
    {
        var user = await database.Users.AsNoTracking().SingleOrDefaultAsync(item => item.Id == administratorId &&
            item.IsActive && item.Role == AccountRoles.Administrator, cancellationToken);
        if (user is null)
        {
            return new(ClosurePasswordOutcome.NotCustomer);
        }

        return ClosurePasswords.Verify(user, password, passwords, missingHash);
    }

    public Task<IReadOnlyList<Guid>> UnreleasedBillRequestsAsync(Guid customerId,
        CancellationToken cancellationToken) =>
        ClosureBillQuery.UnreleasedRequestsAsync(database, customerId, cancellationToken);

    public Task<AccountErasureResult> EraseCustomerAsync(Guid administratorId, Guid customerId,
        AccountErasureRequest request, string verifiedSecurityStamp, CancellationToken cancellationToken) =>
        ClosureLocks.RunGuardedAsync(database, cleanup,
            () => EraseCustomerLockedAsync(administratorId, customerId, request, verifiedSecurityStamp,
                cancellationToken),
            result => result.Receipt, new AccountErasureResult(AccountErasureStatus.RetryLater));

    private async Task<AccountErasureResult> EraseCustomerLockedAsync(Guid administratorId, Guid customerId,
        AccountErasureRequest request, string verifiedSecurityStamp, CancellationToken cancellationToken)
    {
        // The Administrator's row is share-locked before the customer's so a demoted actor cannot finish an erasure.
        var actor = new CustomerErasureActor(administratorId, request.Reason, administratorId,
            () => CheckAdministratorAsync(administratorId, verifiedSecurityStamp, cancellationToken), null);
        var run = await CustomerErasureCommand.RunAsync(database, customerId, request.OperationId, actor,
            cancellationToken);
        return run.Outcome switch
        {
            CustomerErasureOutcome.Done => await ErasedAsync(customerId, run.ReceiptId, run.RecordedAt, staff: false,
                cancellationToken),
            CustomerErasureOutcome.Blocked => new(AccountErasureStatus.Blocked, run.Blockers),
            CustomerErasureOutcome.Conflict => new(AccountErasureStatus.Conflict),
            CustomerErasureOutcome.ActorForbidden => new(AccountErasureStatus.Forbidden),
            CustomerErasureOutcome.SessionChanged => new(AccountErasureStatus.SessionChanged),
            CustomerErasureOutcome.ProtectedSample => new(AccountErasureStatus.Refused),
            _ => new(AccountErasureStatus.NotFound)
        };
    }

    private async Task<CustomerErasureOutcome?> CheckAdministratorAsync(Guid administratorId,
        string verifiedSecurityStamp, CancellationToken cancellationToken)
    {
        var actor = await database.Users.FromSqlInterpolated(
            $"SELECT * FROM \"AspNetUsers\" WHERE \"Id\" = {administratorId} FOR SHARE").AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        if (actor is not { IsActive: true, Role: AccountRoles.Administrator })
        {
            return CustomerErasureOutcome.ActorForbidden;
        }

        return actor.SecurityStamp != verifiedSecurityStamp ? CustomerErasureOutcome.SessionChanged : null;
    }

    public Task<AccountErasureResult> EraseStaffAsync(Guid administratorId, Guid staffId,
        AccountErasureRequest request, string verifiedSecurityStamp, CancellationToken cancellationToken) =>
        ClosureLocks.RunGuardedAsync(database, cleanup,
            () => EraseStaffLockedAsync(administratorId, staffId, request, verifiedSecurityStamp, cancellationToken),
            result => result.Receipt, new AccountErasureResult(AccountErasureStatus.RetryLater));

    // The Administrator rows lock in id order with the target, so two erasures can never leave no Administrator.
    private async Task<AccountErasureResult> EraseStaffLockedAsync(Guid administratorId, Guid staffId,
        AccountErasureRequest request, string verifiedSecurityStamp, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        await ClosureLocks.LockOwnerAsync(database, staffId, cancellationToken);
        var locked = await AdministratorScope.LockTargetAsync(database, administratorId, staffId, request.OperationId,
            lockAdministrators: true, cancellationToken);
        switch (locked.Outcome)
        {
            case AdministratorTargetOutcome.Forbidden:
                return new(AccountErasureStatus.Forbidden);
            case AdministratorTargetOutcome.Replayed:
                var applied = await FindReceiptAsync(administratorId, staffId, request.OperationId, cancellationToken);
                return applied ?? new(AccountErasureStatus.Conflict);
            case AdministratorTargetOutcome.NotFound:
                return new(AccountErasureStatus.NotFound);
        }

        var actor = locked.Administrators!.Single(item => item.Id == administratorId);
        if (actor.SecurityStamp != verifiedSecurityStamp)
        {
            return new(AccountErasureStatus.SessionChanged);
        }

        var user = locked.User!;
        if (user.Role == AccountRoles.Customer)
        {
            return new(AccountErasureStatus.NotFound);
        }

        if (user.Id == administratorId || SampleAccountProtection.IsProtectedStaff(user.Id) ||
            (user.Role == AccountRoles.Administrator && user.IsActive &&
                !locked.Administrators!.Any(item => item.Id != user.Id)))
        {
            return new(AccountErasureStatus.Refused);
        }

        var now = await database.DatabaseNowAsync(cancellationToken);
        var blockers = await StaffWorkBlockers.ReadAsync(database, staffId, now, cancellationToken);
        if (blockers.Count > 0)
        {
            return new(AccountErasureStatus.Blocked, null, blockers);
        }

        var profile = await database.StaffProfiles.FromSqlInterpolated(
            $"SELECT * FROM \"StaffProfiles\" WHERE \"UserId\" = {staffId} FOR UPDATE").SingleAsync(cancellationToken);
        var recorded = UtcTime.TruncateToMicroseconds(now);
        await StaffErasure.ApplyAsync(database, staffId, profile, request.OperationId, administratorId, request.Reason,
            recorded, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await ErasedAsync(staffId, request.OperationId, recorded, staff: true, cancellationToken);
    }

    private async Task<AccountErasureResult> ErasedAsync(Guid userId, Guid receiptId, DateTimeOffset erasedAt,
        bool staff, CancellationToken cancellationToken)
    {
        var pending = await CustomerErasure.FileRemovalPendingAsync(database, userId, cancellationToken);
        var years = await evidencePeriod.YearsAsync(cancellationToken);
        var retained = staff ? ClosureRetention.StaffCategories(years) : ClosureRetention.Categories(years);
        return new(AccountErasureStatus.Erased, null, null, new CustomerClosureReceipt(receiptId, erasedAt, pending,
            retained));
    }
}

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
    MissingAccountPasswordHash missingHash, PhotoCleanupSignal cleanup) : IAccountErasureStore
{
    public Task<bool> IsAdministratorAsync(Guid administratorId, CancellationToken cancellationToken) =>
        AdministratorScope.IsAdministratorAsync(database, administratorId, cancellationToken);

    public async Task<AccountErasureResult?> FindReceiptAsync(Guid administratorId, Guid targetId, Guid operationId,
        CancellationToken cancellationToken)
    {
        var receipt = await database.AccountEvents.AsNoTracking().SingleOrDefaultAsync(item => item.Id == operationId &&
            item.TargetId == targetId && item.Kind == AccountEventKind.Erased && item.ActorId == administratorId,
            cancellationToken);
        return receipt is null ? null : await ErasedAsync(targetId, receipt.Id, receipt.RecordedAt, cancellationToken);
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

        if (user.PasswordHash is null)
        {
            missingHash.Verify(passwords, password);
            return new(ClosurePasswordOutcome.WrongPassword);
        }

        return passwords.VerifyHashedPassword(user, user.PasswordHash, password) == PasswordVerificationResult.Failed
            ? new(ClosurePasswordOutcome.WrongPassword)
            : new(ClosurePasswordOutcome.Verified, user.SecurityStamp);
    }

    public async Task<IReadOnlyList<Guid>> UnreleasedBillRequestsAsync(Guid customerId,
        CancellationToken cancellationToken)
    {
        return await (from payment in database.ReservationDepositPayments.AsNoTracking()
                      join request in database.ReservationDepositRequests.AsNoTracking() on payment.RequestId equals request.Id
                      where request.CustomerId == customerId && payment.State == DepositPaymentState.Pending &&
                          payment.BillDeletedAt == null
                      select payment.RequestId).ToListAsync(cancellationToken);
    }

    public async Task<AccountErasureResult> EraseCustomerAsync(Guid administratorId, Guid customerId,
        AccountErasureRequest request, string verifiedSecurityStamp, CancellationToken cancellationToken)
    {
        try
        {
            var result = await EraseCustomerLockedAsync(administratorId, customerId, request, verifiedSecurityStamp,
                cancellationToken);
            if (result.Receipt is { FileRemovalPending: true })
            {
                cleanup.Request();
            }

            return result;
        }
        catch (Exception failure) when (ClosureLocks.IsLockConflict(failure))
        {
            database.ChangeTracker.Clear();
            return new(AccountErasureStatus.RetryLater);
        }
    }

    private async Task<AccountErasureResult> EraseCustomerLockedAsync(Guid administratorId, Guid customerId,
        AccountErasureRequest request, string verifiedSecurityStamp, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        await ClosureLocks.LockOwnerAsync(database, customerId, cancellationToken);

        // A replay must match the kind, the target and the Administrator who asked.
        var receipt = await database.AccountEvents.AsNoTracking().SingleOrDefaultAsync(item =>
            item.Id == request.OperationId && item.TargetId == customerId && item.Kind == AccountEventKind.Erased &&
            item.ActorId == administratorId, cancellationToken);
        if (receipt is not null)
        {
            return await ErasedAsync(customerId, receipt.Id, receipt.RecordedAt, cancellationToken);
        }

        var actor = await database.Users.FromSqlInterpolated(
            $"SELECT * FROM \"AspNetUsers\" WHERE \"Id\" = {administratorId} FOR SHARE").AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        if (actor is not { IsActive: true, Role: AccountRoles.Administrator })
        {
            return new(AccountErasureStatus.Forbidden);
        }

        if (actor.SecurityStamp != verifiedSecurityStamp)
        {
            return new(AccountErasureStatus.SessionChanged);
        }

        var user = await database.Users.FromSqlInterpolated(
            $"SELECT * FROM \"AspNetUsers\" WHERE \"Id\" = {customerId} FOR UPDATE").AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        if (user is not { Role: AccountRoles.Customer, ClosedAt: null })
        {
            return new(AccountErasureStatus.NotFound);
        }

        await ClosureLocks.LockObligationsAsync(database, customerId, cancellationToken);
        var now = await database.DatabaseNowAsync(cancellationToken);
        var blockers = await CustomerClosureBlockers.ReadAsync(database, customerId, now, cancellationToken);
        if (blockers.Count > 0)
        {
            return new(AccountErasureStatus.Blocked, blockers);
        }

        var profile = await database.CustomerProfiles.FromSqlInterpolated(
            $"SELECT * FROM \"CustomerProfiles\" WHERE \"UserId\" = {customerId} FOR UPDATE")
            .SingleAsync(cancellationToken);
        if (SampleAccountProtection.IsProtectedCustomer(profile.RegistrationKey))
        {
            return new(AccountErasureStatus.Refused);
        }

        var recorded = UtcTime.TruncateToMicroseconds(now);
        await CustomerErasure.ApplyAsync(database, customerId, profile, request.OperationId, administratorId,
            request.Reason, recorded, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await ErasedAsync(customerId, request.OperationId, recorded, cancellationToken);
    }

    private async Task<AccountErasureResult> ErasedAsync(Guid userId, Guid receiptId, DateTimeOffset erasedAt,
        CancellationToken cancellationToken)
    {
        var pending = await CustomerErasure.FileRemovalPendingAsync(database, userId, cancellationToken);
        return new(AccountErasureStatus.Erased, null, null,
            new CustomerClosureReceipt(receiptId, erasedAt, pending, ClosureRetention.Categories));
    }
}

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

// Lock order: picture owner advisory, closure advisory, user, vehicles by id with due holds released, then the
// customer's deposit requests and payments, then one blocker statement. Provider I/O never runs inside it.
public sealed class CustomerClosureStore(DealershipDbContext database,
    IPasswordHasher<AccountUser> passwords, MissingAccountPasswordHash missingHash, PhotoCleanupSignal cleanup,
    IBusinessEvidencePeriod evidencePeriod)
    : ICustomerClosureStore
{
    public async Task<IReadOnlyList<ClosureBlocker>?> BlockersAsync(Guid customerId,
        CancellationToken cancellationToken)
    {
        var open = await database.Users.AsNoTracking().AnyAsync(user => user.Id == customerId &&
            user.Role == AccountRoles.Customer && user.ClosedAt == null, cancellationToken);
        return open
            ? await CustomerClosureBlockers.ReadAsync(database, customerId, await database.DatabaseNowAsync(cancellationToken),
                cancellationToken)
            : null;
    }

    public async Task<ClosurePasswordCheck> VerifyPasswordAsync(Guid customerId, string password,
        CancellationToken cancellationToken)
    {
        var user = await database.Users.AsNoTracking().SingleOrDefaultAsync(item => item.Id == customerId &&
            item.Role == AccountRoles.Customer && item.ClosedAt == null, cancellationToken);
        if (user is null)
        {
            return new(ClosurePasswordOutcome.NotCustomer);
        }

        var registrationKey = await database.CustomerProfiles.AsNoTracking().Where(item => item.UserId == customerId)
            .Select(item => item.RegistrationKey).SingleOrDefaultAsync(cancellationToken);
        if (SampleAccountProtection.IsProtectedCustomer(registrationKey))
        {
            return new(ClosurePasswordOutcome.ProtectedSample);
        }

        return ClosurePasswords.Verify(user, password, passwords, missingHash);
    }

    public Task<IReadOnlyList<Guid>> UnreleasedBillRequestsAsync(Guid customerId,
        CancellationToken cancellationToken) =>
        ClosureBillQuery.UnreleasedRequestsAsync(database, customerId, cancellationToken);

    public Task<CustomerClosureResult> CloseAsync(Guid customerId, Guid operationId, string verifiedSecurityStamp,
        CancellationToken cancellationToken) =>
        ClosureLocks.RunGuardedAsync(database, cleanup,
            () => CloseLockedAsync(customerId, operationId, verifiedSecurityStamp, cancellationToken),
            result => result.Receipt, new CustomerClosureResult(CustomerClosureStatus.RetryLater));

    private async Task<CustomerClosureResult> CloseLockedAsync(Guid customerId, Guid operationId,
        string verifiedSecurityStamp, CancellationToken cancellationToken)
    {
        // The customer is their own actor, so the target row lock is the only actor lock and carries the stamp.
        var actor = new CustomerErasureActor(customerId, null, null, null, verifiedSecurityStamp);
        var run = await CustomerErasureCommand.RunAsync(database, customerId, operationId, actor, cancellationToken);
        return run.Outcome switch
        {
            CustomerErasureOutcome.Done => await ClosedAsync(customerId, run.ReceiptId, run.RecordedAt, cancellationToken),
            CustomerErasureOutcome.Blocked => new(CustomerClosureStatus.Blocked, run.Blockers),
            CustomerErasureOutcome.Conflict => new(CustomerClosureStatus.Conflict),
            CustomerErasureOutcome.SessionChanged => new(CustomerClosureStatus.SessionChanged),
            CustomerErasureOutcome.ProtectedSample => new(CustomerClosureStatus.ProtectedSample),
            _ => new(CustomerClosureStatus.Forbidden)
        };
    }

    // The receipt says pending while any picture of the customer still waits for its object to be deleted.
    private async Task<CustomerClosureResult> ClosedAsync(Guid customerId, Guid receiptId, DateTimeOffset closedAt,
        CancellationToken cancellationToken)
    {
        var pending = await CustomerErasure.FileRemovalPendingAsync(database, customerId, cancellationToken);
        var years = await evidencePeriod.YearsAsync(cancellationToken);
        return new(CustomerClosureStatus.Closed, null,
            new CustomerClosureReceipt(receiptId, closedAt, pending, ClosureRetention.Categories(years)));
    }
}

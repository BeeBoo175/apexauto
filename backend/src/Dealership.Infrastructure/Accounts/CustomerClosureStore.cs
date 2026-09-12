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
using Npgsql;

namespace Dealership.Infrastructure.Accounts;

// Lock order: picture owner advisory, closure advisory, user, vehicles by id with due holds released, then the
// customer's deposit requests and payments, then one blocker statement. Provider I/O never runs inside it.
public sealed class CustomerClosureStore(DealershipDbContext database,
    IPasswordHasher<AccountUser> passwords, MissingAccountPasswordHash missingHash, PhotoCleanupSignal cleanup)
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

    public async Task<CustomerClosureResult> CloseAsync(Guid customerId, Guid operationId, string verifiedSecurityStamp,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await CloseLockedAsync(customerId, operationId, verifiedSecurityStamp, cancellationToken);
            if (result.Receipt is { FileRemovalPending: true })
            {
                cleanup.Request();
            }

            return result;
        }
        catch (Exception failure) when (IsLockConflict(failure))
        {
            database.ChangeTracker.Clear();
            return new(CustomerClosureStatus.RetryLater);
        }
    }

    private async Task<CustomerClosureResult> CloseLockedAsync(Guid customerId, Guid operationId,
        string verifiedSecurityStamp, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        await database.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({"account-picture:" + customerId}, 0))", cancellationToken);
        await database.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({"account-closure:" + customerId}, 0))", cancellationToken);

        // A replay must match both the kind and the target, never just the operation id.
        var receipt = await database.AccountEvents.AsNoTracking().SingleOrDefaultAsync(item => item.Id == operationId &&
            item.TargetId == customerId && item.Kind == AccountEventKind.Erased, cancellationToken);
        if (receipt is not null)
        {
            return await ClosedAsync(customerId, receipt.Id, receipt.RecordedAt, cancellationToken);
        }

        var user = await database.Users.FromSqlInterpolated(
            $"SELECT * FROM \"AspNetUsers\" WHERE \"Id\" = {customerId} FOR UPDATE").AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        if (user is not { Role: AccountRoles.Customer, ClosedAt: null })
        {
            return new(CustomerClosureStatus.Forbidden);
        }

        if (user.SecurityStamp != verifiedSecurityStamp)
        {
            return new(CustomerClosureStatus.SessionChanged);
        }

        await LockObligationsAsync(customerId, cancellationToken);
        var now = await database.DatabaseNowAsync(cancellationToken);
        var blockers = await CustomerClosureBlockers.ReadAsync(database, customerId, now, cancellationToken);
        if (blockers.Count > 0)
        {
            return new(CustomerClosureStatus.Blocked, blockers);
        }

        var profile = await database.CustomerProfiles.FromSqlInterpolated(
            $"SELECT * FROM \"CustomerProfiles\" WHERE \"UserId\" = {customerId} FOR UPDATE")
            .SingleAsync(cancellationToken);
        if (SampleAccountProtection.IsProtectedCustomer(profile.RegistrationKey))
        {
            return new(CustomerClosureStatus.ProtectedSample);
        }

        await CustomerErasure.ApplyAsync(database, customerId, profile, operationId, customerId, null,
            UtcTime.TruncateToMicroseconds(now), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await ClosedAsync(customerId, operationId, UtcTime.TruncateToMicroseconds(now), cancellationToken);
    }

    private async Task LockObligationsAsync(Guid customerId, CancellationToken cancellationToken)
    {
        var vehicleIds = await database.Reservations.Where(item => item.CustomerId == customerId && item.IsActive)
            .Select(item => item.VehicleId).Distinct().ToArrayAsync(cancellationToken);
        if (vehicleIds.Length > 0)
        {
            var vehicles = await database.Vehicles.FromSqlInterpolated(
                $"SELECT * FROM \"Vehicles\" WHERE \"Id\" = ANY({vehicleIds}) ORDER BY \"Id\" FOR UPDATE")
                .AsNoTracking().ToListAsync(cancellationToken);
            foreach (var vehicle in vehicles)
            {
                await database.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT release_unavailable_reservation({vehicle.Id})", cancellationToken);
            }
        }

        var requestIds = await database.Database.SqlQuery<Guid>(
            $"SELECT \"Id\" AS \"Value\" FROM \"ReservationDepositRequests\" WHERE \"CustomerId\" = {customerId} ORDER BY \"Id\" FOR UPDATE")
            .ToListAsync(cancellationToken);
        if (requestIds.Count > 0)
        {
            var ids = requestIds.ToArray();
            await database.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM \"ReservationDepositPayments\" WHERE \"RequestId\" = ANY({ids}) ORDER BY \"RequestId\" FOR UPDATE",
                cancellationToken);
        }
    }

    // The receipt says pending while any picture of the customer still waits for its object to be deleted.
    private async Task<CustomerClosureResult> ClosedAsync(Guid customerId, Guid receiptId, DateTimeOffset closedAt,
        CancellationToken cancellationToken)
    {
        var pending = await database.AccountPictureRemovals.AsNoTracking().AnyAsync(removal =>
            removal.DeletedAt == null && database.AccountPictures.Any(picture => picture.Id == removal.PictureId &&
                picture.UserId == customerId), cancellationToken);
        return new(CustomerClosureStatus.Closed, null,
            new CustomerClosureReceipt(receiptId, closedAt, pending, ClosureRetention.Categories));
    }

    private static bool IsLockConflict(Exception failure)
    {
        for (var current = failure; current is not null; current = current.InnerException)
        {
            if (current is PostgresException
                {
                    SqlState: PostgresErrorCodes.DeadlockDetected or
                PostgresErrorCodes.SerializationFailure
                })
            {
                return true;
            }
        }

        return false;
    }
}

using Dealership.Application.Accounts;
using Dealership.Domain.Accounts;
using Dealership.Domain.Common;
using Dealership.Infrastructure.Identity;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Dealership.Infrastructure.Accounts;

public sealed class CustomerSupportStore(DealershipDbContext database, TimeProvider clock) : ICustomerSupportStore
{
    public async Task<NricRevealResult> RevealNricAsync(Guid administratorId, Guid customerId,
        CustomerSupportRequest request, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        if (!await AdministratorScope.LockAdministratorAsync(database, administratorId, cancellationToken))
        {
            return new(CustomerSupportStatus.Forbidden);
        }

        var nric = await (from user in database.Users.AsNoTracking()
                          where user.Id == customerId && user.Role == AccountRoles.Customer && user.ClosedAt == null
                          join profile in database.CustomerProfiles.AsNoTracking() on user.Id equals profile.UserId
                          select profile.Nric).SingleOrDefaultAsync(cancellationToken);
        if (nric is null)
        {
            return new(CustomerSupportStatus.NotFound);
        }

        // The event is written before the number is returned, in the same transaction: no reveal goes unlogged, and
        // a repeated operation id reveals again without a second event.
        if (!await database.AccountEvents.AnyAsync(item => item.Id == request.OperationId, cancellationToken))
        {
            database.AccountEvents.Add(new AccountEvent(request.OperationId, customerId, administratorId,
                AccountEventKind.NricRevealed, request.Reason, null, Now()));
            await database.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return new(CustomerSupportStatus.Succeeded, nric);
    }

    public async Task<CustomerSupportStatus> RevokeSessionsAsync(Guid administratorId, Guid customerId,
        CustomerSupportRequest request, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        if (!await AdministratorScope.LockAdministratorAsync(database, administratorId, cancellationToken))
        {
            return CustomerSupportStatus.Forbidden;
        }

        if (await database.AccountEvents.AnyAsync(item => item.Id == request.OperationId && item.TargetId == customerId,
                cancellationToken))
        {
            return CustomerSupportStatus.Succeeded;
        }

        var user = await database.Users.FromSqlInterpolated(
            $"SELECT * FROM \"AspNetUsers\" WHERE \"Id\" = {customerId} FOR UPDATE").SingleOrDefaultAsync(cancellationToken);
        if (user is not { Role: AccountRoles.Customer, ClosedAt: null })
        {
            return CustomerSupportStatus.NotFound;
        }

        var now = Now();
        user.SecurityStamp = Guid.NewGuid().ToString();
        await AccountAccessRevocation.RevokeSessionsAsync(database, customerId, now, cancellationToken);
        database.AccountEvents.Add(new AccountEvent(request.OperationId, customerId, administratorId,
            AccountEventKind.SessionsRevoked, request.Reason, null, now));
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return CustomerSupportStatus.Succeeded;
    }

    public async Task<(CustomerSupportStatus Status, CustomerContact? Contact)> ReadContactAsync(Guid administratorId,
        Guid customerId, CancellationToken cancellationToken)
    {
        if (!await AdministratorScope.IsAdministratorAsync(database, administratorId, cancellationToken))
        {
            return (CustomerSupportStatus.Forbidden, null);
        }

        var contact = await database.Users.AsNoTracking()
            .Where(user => user.Id == customerId && user.Role == AccountRoles.Customer && user.ClosedAt == null)
            .Select(user => new CustomerContact(user.Email!, user.EmailConfirmed, user.IsActive))
            .SingleOrDefaultAsync(cancellationToken);
        return contact is null ? (CustomerSupportStatus.NotFound, null) : (CustomerSupportStatus.Succeeded, contact);
    }

    public async Task<CustomerSupportStatus> RecordAsync(Guid administratorId, Guid customerId, AccountEventKind kind,
        CustomerSupportRequest request, CancellationToken cancellationToken)
    {
        database.AccountEvents.Add(new AccountEvent(request.OperationId, customerId, administratorId, kind,
            request.Reason, null, Now()));
        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // The same operation was already recorded, which is the retry this identity exists to absorb.
            database.ChangeTracker.Clear();
        }

        return CustomerSupportStatus.Succeeded;
    }

    private DateTimeOffset Now() => UtcTime.TruncateToMicroseconds(clock.GetUtcNow());
}

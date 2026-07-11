using Dealership.Application.Accounts;
using Dealership.Domain.Accounts;
using Dealership.Domain.Common;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Dealership.Infrastructure.Accounts;

public sealed class AccountUsernameStore(DealershipDbContext database, TimeProvider clock) : IAccountUsernameStore
{
    private const string UsernameIndex = "AccountUsernameIndex";

    public async Task<AccountUsernameStatus> ChangeAsync(Guid userId, Guid operationId, string username,
        CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var user = await database.Users.FromSqlInterpolated(
                $"SELECT * FROM \"AspNetUsers\" WHERE \"Id\" = {userId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);

            if (user is not { IsActive: true })
            {
                return AccountUsernameStatus.Forbidden;
            }

            // The operation identity is the event identity, so an applied change is recognised on retry.
            if (await database.AccountEvents.AnyAsync(item => item.Id == operationId && item.TargetId == userId,
                    cancellationToken))
            {
                return AccountUsernameStatus.Changed;
            }

            if (user.AccountUsername == username)
            {
                return AccountUsernameStatus.Changed;
            }

            var before = user.AccountUsername;
            user.AccountUsername = username;
            database.AccountEvents.Add(new AccountEvent(operationId, userId, userId, AccountEventKind.UsernameChanged,
                null, [new AccountChange("Username", before, username)], Now()));
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return AccountUsernameStatus.Changed;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: UsernameIndex })
        {
            await transaction.RollbackAsync(cancellationToken);
            database.ChangeTracker.Clear();
            return AccountUsernameStatus.Taken;
        }
    }

    private DateTimeOffset Now() => UtcTime.TruncateToMicroseconds(clock.GetUtcNow());
}

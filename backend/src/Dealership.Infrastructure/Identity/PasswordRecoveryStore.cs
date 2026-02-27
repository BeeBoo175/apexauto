using Dealership.Application.Accounts;
using Dealership.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Dealership.Infrastructure.Mail;

namespace Dealership.Infrastructure.Identity;

public sealed class PasswordRecoveryStore(DealershipDbContext database, UserManager<AccountUser> users,
    PasswordResetToken tokens, MailDeliverySignal mail) : IPasswordRecoveryStore
{
    public async Task<IPasswordRecoveryRequestTransaction> LockRequestAsync(string email, CancellationToken cancellationToken)
    {
        var normalized = users.NormalizeEmail(email);
        var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var user = await database.Users.FromSqlInterpolated(
                $"SELECT * FROM \"AspNetUsers\" WHERE \"NormalizedEmail\" = {normalized} FOR UPDATE")
                .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
            var context = new PasswordRecoveryRequestContext(user is not null);
            return new PasswordRecoveryRequestTransaction(database, transaction, context, user, mail);
        }
        catch
        {
            try
            {
                await transaction.DisposeAsync();
            }
            finally
            {
                database.ChangeTracker.Clear();
            }

            throw;
        }
    }

    public async Task<IPasswordResetTransaction?> LockResetAsync(string token, CancellationToken cancellationToken)
    {
        if (!PasswordResetToken.TryReadId(token, out var challengeId))
        {
            return null;
        }

        var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            database.ChangeTracker.Clear();
            var userId = await database.PasswordResetChallenges.Where(challenge => challenge.Id == challengeId)
                .Select(challenge => (Guid?)challenge.UserId).SingleOrDefaultAsync(cancellationToken);
            // This is the login lock too: no old-credential session can commit after the reset.
            var user = userId is null ? null : await database.Users.FromSqlInterpolated(
                $"SELECT * FROM \"AspNetUsers\" WHERE \"Id\" = {userId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);
            var context = new PasswordResetContext(user is not null, user?.Email, user?.SecurityStamp);
            return new PasswordResetTransaction(database, transaction, context, user, users, tokens, token, challengeId);
        }
        catch
        {
            try
            {
                await transaction.DisposeAsync();
            }
            finally
            {
                database.ChangeTracker.Clear();
            }

            throw;
        }
    }
}

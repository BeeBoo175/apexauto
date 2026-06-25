using Dealership.Application.Accounts;
using Dealership.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Dealership.Infrastructure.Mail;

namespace Dealership.Infrastructure.Identity;

public sealed class EmailVerificationStore(DealershipDbContext database, UserManager<AccountUser> users,
    EmailVerificationToken tokens, TimeProvider clock, MailDeliverySignal mail) : IEmailVerificationStore
{
    public async Task QueueInitialAsync(Guid userId, CancellationToken cancellationToken)
    {
        // Registration owns the surrounding transaction; the mail intent commits with the account.
        var user = await database.Users.SingleAsync(account => account.Id == userId, cancellationToken);
        database.EmailVerificationChallenges.Add(EmailVerificationChallenge.Create(user, null, clock.GetUtcNow()));
        await database.SaveChangesAsync(cancellationToken);
    }

    // Customers and staff both verify; each needs the profile its role owns.
    private async Task<EmailVerificationAccount?> AccountOfAsync(AccountUser? user, CancellationToken cancellationToken)
    {
        if (user is null)
        {
            return null;
        }

        var hasProfile = user.Role == AccountRoles.Customer
            ? await database.CustomerProfiles.AnyAsync(profile => profile.UserId == user.Id, cancellationToken)
            : await database.StaffProfiles.AnyAsync(profile => profile.UserId == user.Id, cancellationToken);
        return new EmailVerificationAccount(user.IsActive, user.Role, hasProfile, user.EmailConfirmed);
    }

    public async Task<IEmailVerificationResendTransaction> LockResendAsync(Guid userId, CancellationToken cancellationToken)
    {
        var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var user = await database.Users.FromSqlInterpolated(
                $"SELECT * FROM \"AspNetUsers\" WHERE \"Id\" = {userId} FOR UPDATE")
                .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
            var account = await AccountOfAsync(user, cancellationToken);
            return new EmailVerificationResendTransaction(database, transaction, account, user, clock, mail);
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

    public async Task<IEmailVerificationConfirmationTransaction?> LockConfirmationAsync(string token, CancellationToken cancellationToken)
    {
        if (!EmailVerificationToken.TryReadId(token, out var challengeId))
        {
            return null;
        }

        var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            database.ChangeTracker.Clear();
            var userId = await database.EmailVerificationChallenges.Where(challenge => challenge.Id == challengeId)
                .Select(challenge => (Guid?)challenge.UserId).SingleOrDefaultAsync(cancellationToken);
            var user = userId is null ? null : await database.Users.FromSqlInterpolated(
                $"SELECT * FROM \"AspNetUsers\" WHERE \"Id\" = {userId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);
            var account = await AccountOfAsync(user, cancellationToken);
            var context = new EmailVerificationConfirmationContext(account, user?.Email);
            return new EmailVerificationConfirmationTransaction(database, transaction, context, user, users, tokens, token, challengeId);
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

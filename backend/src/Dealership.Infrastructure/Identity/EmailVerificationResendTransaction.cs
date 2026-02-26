using Dealership.Application.Accounts;
using Dealership.Infrastructure.Mail;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Dealership.Infrastructure.Identity;

internal sealed class EmailVerificationResendTransaction(DealershipDbContext database, IDbContextTransaction transaction,
    EmailVerificationCustomer? customer, AccountUser? user, TimeProvider clock, MailDeliverySignal mail)
    : IEmailVerificationResendTransaction
{
    public EmailVerificationCustomer? Customer => customer;

    public Task<Guid?> ReadResendOwnerAsync(Guid resendKey, CancellationToken cancellationToken)
    {
        return database.EmailVerificationChallenges
            .Where(challenge => challenge.ResendKey == resendKey)
            .Select(challenge => (Guid?)challenge.UserId).SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DateTimeOffset>> ReadRecentResendsAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        return await database.EmailVerificationChallenges
            .Where(challenge => challenge.UserId == user!.Id && challenge.ResendKey != null &&
                challenge.CreatedAt > now - EmailVerificationPolicy.ResendWindow)
            .OrderByDescending(challenge => challenge.CreatedAt)
            .Select(challenge => challenge.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task CommitAsync(Guid resendKey, DateTimeOffset invalidatedAt, CancellationToken cancellationToken)
    {
        await database.EmailVerificationChallenges
            .Where(challenge => challenge.UserId == user!.Id && challenge.InvalidatedAt == null && challenge.ConsumedAt == null)
            .ExecuteUpdateAsync(update => update.SetProperty(challenge => challenge.InvalidatedAt, invalidatedAt), cancellationToken);
        database.EmailVerificationChallenges.Add(EmailVerificationChallenge.Create(user!, resendKey, clock.GetUtcNow()));
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        mail.Request();
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await transaction.DisposeAsync();
        }
        finally
        {
            database.ChangeTracker.Clear();
        }
    }
}

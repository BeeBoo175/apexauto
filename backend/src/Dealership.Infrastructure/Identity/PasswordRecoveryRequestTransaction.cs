using Dealership.Application.Accounts;
using Dealership.Infrastructure.Mail;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Dealership.Infrastructure.Identity;

internal sealed class PasswordRecoveryRequestTransaction(DealershipDbContext database, IDbContextTransaction transaction,
    PasswordRecoveryRequestContext context, AccountUser? user, MailDeliverySignal mail) : IPasswordRecoveryRequestTransaction
{
    public PasswordRecoveryRequestContext Context => context;

    public async Task<IReadOnlyList<DateTimeOffset>> ReadRecentRequestsAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        return await database.PasswordResetChallenges
            .Where(challenge => challenge.UserId == user!.Id && challenge.CreatedAt > now - PasswordRecoveryPolicy.RequestWindow)
            .OrderByDescending(challenge => challenge.CreatedAt)
            .Select(challenge => challenge.CreatedAt).Take(PasswordRecoveryPolicy.MaximumRequestsPerWindow).ToListAsync(cancellationToken);
    }

    public async Task CommitAsync(DateTimeOffset requestedAt, CancellationToken cancellationToken)
    {
        await database.PasswordResetChallenges
            .Where(challenge => challenge.UserId == user!.Id && challenge.InvalidatedAt == null && challenge.ConsumedAt == null)
            .ExecuteUpdateAsync(update => update.SetProperty(challenge => challenge.InvalidatedAt, requestedAt), cancellationToken);
        database.PasswordResetChallenges.Add(new PasswordResetChallenge
        {
            Id = Guid.NewGuid(),
            UserId = user!.Id,
            RecipientEmail = user.Email!,
            CreatedAt = requestedAt,
            ExpiresAt = requestedAt + PasswordRecoveryPolicy.Lifetime,
            NextDeliveryAt = requestedAt
        });
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

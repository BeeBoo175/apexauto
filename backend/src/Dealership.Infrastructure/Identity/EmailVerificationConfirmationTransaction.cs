using Dealership.Application.Accounts;
using Dealership.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Dealership.Infrastructure.Identity;

internal sealed class EmailVerificationConfirmationTransaction(DealershipDbContext database, IDbContextTransaction transaction,
    EmailVerificationConfirmationContext context, AccountUser? user, UserManager<AccountUser> users,
    EmailVerificationToken tokens, string token, Guid challengeId) : IEmailVerificationConfirmationTransaction
{
    private EmailVerificationChallenge? _challenge;

    public EmailVerificationConfirmationContext Context => context;

    public async Task<EmailVerificationConfirmationChallenge> ReadChallengeAsync(CancellationToken cancellationToken)
    {
        _challenge = await database.EmailVerificationChallenges.FromSqlInterpolated(
            $"SELECT * FROM \"EmailVerificationChallenges\" WHERE \"Id\" = {challengeId} FOR UPDATE")
            .SingleAsync(cancellationToken);
        return new EmailVerificationConfirmationChallenge(tokens.Verify(token, _challenge), _challenge.RecipientEmail ?? string.Empty,
            _challenge.ExpiresAt, _challenge.InvalidatedAt, _challenge.ConsumedAt);
    }

    public async Task CommitAsync(DateTimeOffset consumedAt, CancellationToken cancellationToken)
    {
        user!.EmailConfirmed = true;
        var confirmed = await users.UpdateAsync(user);

        if (!confirmed.Succeeded)
        {
            throw new InvalidOperationException("Identity could not confirm the account email.");
        }

        _challenge!.ConsumedAt = consumedAt;
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
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

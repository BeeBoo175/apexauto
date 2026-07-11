using Dealership.Application.Accounts;
using Dealership.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Dealership.Infrastructure.Identity;

internal sealed class PasswordResetTransaction(DealershipDbContext database, IDbContextTransaction transaction,
    PasswordResetContext context, AccountUser? user, UserManager<AccountUser> users,
    PasswordResetToken tokens, string token, Guid challengeId) : IPasswordResetTransaction
{
    private PasswordResetChallenge? _challenge;

    public PasswordResetContext Context => context;

    public async Task<PasswordResetChallengeContext> ReadChallengeAsync(CancellationToken cancellationToken)
    {
        _challenge = await database.PasswordResetChallenges.FromSqlInterpolated(
            $"SELECT * FROM \"PasswordResetChallenges\" WHERE \"Id\" = {challengeId} FOR UPDATE")
            .SingleAsync(cancellationToken);
        return new PasswordResetChallengeContext(tokens.Verify(token, _challenge), _challenge.RecipientEmail,
            _challenge.ExpiresAt, _challenge.InvalidatedAt, _challenge.ConsumedAt, _challenge.CompletedSecurityStamp);
    }

    public bool VerifyPassword(string password)
    {
        // Verify the current slow Identity hash without CheckPasswordAsync's possible rehash/write.
        return user!.PasswordHash is not null &&
            users.PasswordHasher.VerifyHashedPassword(user, user.PasswordHash, password) != PasswordVerificationResult.Failed;
    }

    public async Task<string[]> ValidatePasswordAsync(string password)
    {
        var errors = new List<string>();

        foreach (var validator in users.PasswordValidators)
        {
            var result = await validator.ValidateAsync(users, user!, password);

            if (!result.Succeeded)
            {
                var descriptions = result.Errors.Select(error => error.Description).ToArray();
                errors.AddRange(descriptions.Length > 0 ? descriptions : ["The password does not meet account policy."]);
            }
        }

        return [.. errors.Distinct(StringComparer.Ordinal)];
    }

    public async Task CommitAsync(string password, DateTimeOffset resetAt, CancellationToken cancellationToken)
    {
        user!.PasswordHash = users.PasswordHasher.HashPassword(user, password);
        user.SecurityStamp = Guid.NewGuid().ToString();
        var updated = await users.UpdateAsync(user);

        if (!updated.Succeeded)
        {
            throw new InvalidOperationException("Identity could not update the recovered credentials.");
        }

        await AccountAccessRevocation.RevokeSessionsAsync(database, user.Id, resetAt, cancellationToken);
        _challenge!.ConsumedAt = resetAt;
        _challenge.CompletedSecurityStamp = user.SecurityStamp;
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

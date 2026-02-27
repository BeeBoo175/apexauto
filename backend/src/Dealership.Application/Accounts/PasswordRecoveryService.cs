namespace Dealership.Application.Accounts;

public sealed class PasswordRecoveryService(IPasswordRecoveryStore store, TimeProvider clock)
{
    public async Task RequestAsync(PasswordRecoveryRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email?.Trim();

        if (string.IsNullOrEmpty(email) || email.Length > AccountCredentialPolicy.EmailMaximumLength)
        {
            return;
        }

        await using var transaction = await store.LockRequestAsync(email, cancellationToken);

        if (!transaction.Context.AccountExists)
        {
            return;
        }

        var now = clock.GetUtcNow();
        var recent = await transaction.ReadRecentRequestsAsync(now, cancellationToken);

        if (!PasswordRecoveryPolicy.CanRequest(now, recent))
        {
            return;
        }

        await transaction.CommitAsync(now, cancellationToken);
    }

    public async Task<PasswordResetResult> ResetAsync(PasswordResetRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.Token) || request.Token.Length > 160 ||
            string.IsNullOrEmpty(request.Password) || request.Password.Length > AccountCredentialPolicy.PasswordMaximumLength)
        {
            return new PasswordResetResult(PasswordResetStatus.Invalid);
        }

        await using var transaction = await store.LockResetAsync(request.Token, cancellationToken);
        var context = transaction?.Context;

        if (context is not { AccountExists: true })
        {
            return new PasswordResetResult(PasswordResetStatus.Invalid);
        }

        var challenge = await transaction!.ReadChallengeAsync(cancellationToken);
        var now = clock.GetUtcNow();

        if (!challenge.TokenValid || challenge.InvalidatedAt is not null || challenge.ExpiresAt <= now ||
            !string.Equals(context.Email, challenge.RecipientEmail, StringComparison.Ordinal))
        {
            return new PasswordResetResult(PasswordResetStatus.Invalid);
        }

        if (challenge.ConsumedAt is not null)
        {
            var matches = challenge.CompletedSecurityStamp == context.SecurityStamp && transaction.VerifyPassword(request.Password);
            return new PasswordResetResult(matches ? PasswordResetStatus.Succeeded : PasswordResetStatus.Invalid);
        }

        var errors = await transaction.ValidatePasswordAsync(request.Password);

        if (errors.Length > 0)
        {
            return new PasswordResetResult(PasswordResetStatus.PasswordRejected, errors);
        }

        await transaction.CommitAsync(request.Password, now, cancellationToken);
        return new PasswordResetResult(PasswordResetStatus.Succeeded);
    }
}

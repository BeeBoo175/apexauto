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

    public Task<PasswordResetResult> ResetAsync(PasswordResetRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.Token) || request.Token.Length > 160 ||
            string.IsNullOrEmpty(request.Password) || request.Password.Length > AccountCredentialPolicy.PasswordMaximumLength)
        {
            return Task.FromResult(new PasswordResetResult(PasswordResetStatus.Invalid));
        }

        return store.ResetAsync(request.Token, request.Password, cancellationToken);
    }
}

namespace Dealership.Application.Accounts;

public sealed class PasswordRecoveryService(IPasswordRecoveryStore store)
{
    public async Task RequestAsync(PasswordRecoveryRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email?.Trim();

        if (string.IsNullOrEmpty(email) || email.Length > AccountCredentialPolicy.EmailMaximumLength)
        {
            return;
        }

        await store.RequestAsync(email, cancellationToken);
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

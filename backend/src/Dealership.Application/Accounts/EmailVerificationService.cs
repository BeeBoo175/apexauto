namespace Dealership.Application.Accounts;

public sealed class EmailVerificationService(IEmailVerificationStore store)
{
    public Task<VerificationResult> ResendAsync(Guid userId, Guid resendKey, CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty)
        {
            return Task.FromResult(new VerificationResult(VerificationStatus.Forbidden));
        }

        if (resendKey == Guid.Empty)
        {
            return Task.FromResult(new VerificationResult(VerificationStatus.Invalid));
        }

        return store.ResendAsync(userId, resendKey, cancellationToken);
    }

    public Task<VerificationResult> ConfirmAsync(string? token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 160)
        {
            return Task.FromResult(new VerificationResult(VerificationStatus.Invalid));
        }

        return store.ConfirmAsync(token, cancellationToken);
    }
}

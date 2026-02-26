namespace Dealership.Application.Accounts;

public sealed class EmailVerificationService(IEmailVerificationStore store, TimeProvider clock)
{
    public async Task<VerificationResult> ResendAsync(Guid userId, Guid resendKey, CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty)
        {
            return new VerificationResult(VerificationStatus.Forbidden);
        }

        if (resendKey == Guid.Empty)
        {
            return new VerificationResult(VerificationStatus.Invalid);
        }

        await using var transaction = await store.LockResendAsync(userId, cancellationToken);
        var customer = transaction.Customer;

        if (customer is not { IsActive: true, Role: AccountRoles.Customer, HasProfile: true })
        {
            return new VerificationResult(VerificationStatus.Forbidden);
        }

        var replayOwner = await transaction.ReadResendOwnerAsync(resendKey, cancellationToken);

        if (replayOwner is not null)
        {
            return new VerificationResult(replayOwner == userId ? VerificationStatus.Accepted : VerificationStatus.Forbidden);
        }

        if (customer.EmailConfirmed)
        {
            return new VerificationResult(VerificationStatus.Accepted);
        }

        var now = clock.GetUtcNow();
        var recent = await transaction.ReadRecentResendsAsync(now, cancellationToken);
        var delay = EmailVerificationPolicy.ResendDelay(now, recent);

        if (delay is not null)
        {
            return new VerificationResult(VerificationStatus.RateLimited, delay);
        }

        await transaction.CommitAsync(resendKey, now, cancellationToken);
        return new VerificationResult(VerificationStatus.Accepted);
    }

    public async Task<VerificationResult> ConfirmAsync(string? token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 160)
        {
            return new VerificationResult(VerificationStatus.Invalid);
        }

        await using var transaction = await store.LockConfirmationAsync(token, cancellationToken);
        var context = transaction?.Context;
        var customer = context?.Customer;

        if (customer is not { IsActive: true, Role: AccountRoles.Customer, HasProfile: true })
        {
            return new VerificationResult(VerificationStatus.Invalid);
        }

        var challenge = await transaction!.ReadChallengeAsync(cancellationToken);

        if (!challenge.TokenValid || challenge.InvalidatedAt is not null ||
            !string.Equals(context!.Email, challenge.RecipientEmail, StringComparison.Ordinal))
        {
            return new VerificationResult(VerificationStatus.Invalid);
        }

        var now = clock.GetUtcNow();

        if (challenge.ExpiresAt <= now)
        {
            return new VerificationResult(VerificationStatus.Invalid);
        }

        if (challenge.ConsumedAt is not null)
        {
            return new VerificationResult(customer.EmailConfirmed ? VerificationStatus.Accepted : VerificationStatus.Invalid);
        }

        if (customer.EmailConfirmed)
        {
            return new VerificationResult(VerificationStatus.Invalid);
        }

        await transaction.CommitAsync(now, cancellationToken);
        return new VerificationResult(VerificationStatus.Accepted);
    }
}

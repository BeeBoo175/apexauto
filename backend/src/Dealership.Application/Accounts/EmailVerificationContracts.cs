namespace Dealership.Application.Accounts;

public enum VerificationStatus
{
    Accepted,
    Invalid,
    Forbidden,
    RateLimited
}

public sealed record VerificationResult(VerificationStatus Status, TimeSpan? RetryAfter = null);

public interface IEmailVerificationStore
{
    Task QueueInitialAsync(Guid userId, CancellationToken cancellationToken);

    Task<VerificationResult> ResendAsync(Guid userId, Guid resendKey, CancellationToken cancellationToken);

    Task<VerificationResult> ConfirmAsync(string token, CancellationToken cancellationToken);
}

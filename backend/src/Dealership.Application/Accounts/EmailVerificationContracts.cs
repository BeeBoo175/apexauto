namespace Dealership.Application.Accounts;

public enum VerificationStatus
{
    Accepted,
    Invalid,
    Forbidden,
    RateLimited
}

public sealed record VerificationResult(VerificationStatus Status, TimeSpan? RetryAfter = null);

public sealed record EmailVerificationCustomer(bool IsActive, string Role, bool HasProfile, bool EmailConfirmed);

public interface IEmailVerificationResendTransaction : IAsyncDisposable
{
    EmailVerificationCustomer? Customer { get; }

    Task<Guid?> ReadResendOwnerAsync(Guid resendKey, CancellationToken cancellationToken);

    Task<IReadOnlyList<DateTimeOffset>> ReadRecentResendsAsync(DateTimeOffset now, CancellationToken cancellationToken);

    Task CommitAsync(Guid resendKey, DateTimeOffset invalidatedAt, CancellationToken cancellationToken);
}

public interface IEmailVerificationStore
{
    Task QueueInitialAsync(Guid userId, CancellationToken cancellationToken);

    Task<IEmailVerificationResendTransaction> LockResendAsync(Guid userId, CancellationToken cancellationToken);

    Task<VerificationResult> ConfirmAsync(string token, CancellationToken cancellationToken);
}

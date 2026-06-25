namespace Dealership.Application.Accounts;

public enum VerificationStatus
{
    Accepted,
    Invalid,
    Forbidden,
    RateLimited
}

public sealed record VerificationResult(VerificationStatus Status, TimeSpan? RetryAfter = null);

public sealed record EmailVerificationAccount(bool IsActive, string Role, bool HasProfile, bool EmailConfirmed);

public sealed record EmailVerificationConfirmationContext(EmailVerificationAccount? Account, string? Email);

public sealed record EmailVerificationConfirmationChallenge(bool TokenValid, string RecipientEmail,
    DateTimeOffset ExpiresAt, DateTimeOffset? InvalidatedAt, DateTimeOffset? ConsumedAt);

public interface IEmailVerificationConfirmationTransaction : IAsyncDisposable
{
    EmailVerificationConfirmationContext Context { get; }

    Task<EmailVerificationConfirmationChallenge> ReadChallengeAsync(CancellationToken cancellationToken);

    Task CommitAsync(DateTimeOffset consumedAt, CancellationToken cancellationToken);
}

public interface IEmailVerificationResendTransaction : IAsyncDisposable
{
    EmailVerificationAccount? Account { get; }

    Task<Guid?> ReadResendOwnerAsync(Guid resendKey, CancellationToken cancellationToken);

    Task<IReadOnlyList<DateTimeOffset>> ReadRecentResendsAsync(DateTimeOffset now, CancellationToken cancellationToken);

    Task CommitAsync(Guid resendKey, DateTimeOffset invalidatedAt, CancellationToken cancellationToken);
}

public interface IEmailVerificationStore
{
    Task QueueInitialAsync(Guid userId, CancellationToken cancellationToken);

    Task<IEmailVerificationResendTransaction> LockResendAsync(Guid userId, CancellationToken cancellationToken);

    Task<IEmailVerificationConfirmationTransaction?> LockConfirmationAsync(string token, CancellationToken cancellationToken);
}

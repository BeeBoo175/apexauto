namespace Dealership.Application.Accounts;

public sealed record PasswordRecoveryRequest(string? Email);

public sealed record PasswordResetRequest(string? Token, string? Password);

public enum PasswordResetStatus
{
    Succeeded,
    Invalid,
    PasswordRejected
}

public sealed record PasswordResetResult(PasswordResetStatus Status, string[]? PasswordErrors = null);

public sealed record PasswordRecoveryRequestContext(bool AccountExists);

public sealed record PasswordResetContext(bool AccountExists, string? Email, string? SecurityStamp);

public sealed record PasswordResetChallengeContext(bool TokenValid, string RecipientEmail, DateTimeOffset ExpiresAt,
    DateTimeOffset? InvalidatedAt, DateTimeOffset? ConsumedAt, string? CompletedSecurityStamp);

public interface IPasswordResetTransaction : IAsyncDisposable
{
    PasswordResetContext Context { get; }

    Task<PasswordResetChallengeContext> ReadChallengeAsync(CancellationToken cancellationToken);

    bool VerifyPassword(string password);

    Task<string[]> ValidatePasswordAsync(string password);

    Task CommitAsync(string password, DateTimeOffset resetAt, CancellationToken cancellationToken);
}

public interface IPasswordRecoveryRequestTransaction : IAsyncDisposable
{
    PasswordRecoveryRequestContext Context { get; }

    Task<IReadOnlyList<DateTimeOffset>> ReadRecentRequestsAsync(DateTimeOffset now, CancellationToken cancellationToken);

    Task CommitAsync(DateTimeOffset requestedAt, CancellationToken cancellationToken);
}

public interface IPasswordRecoveryStore
{
    Task<IPasswordRecoveryRequestTransaction> LockRequestAsync(string email, CancellationToken cancellationToken);

    Task<IPasswordResetTransaction?> LockResetAsync(string token, CancellationToken cancellationToken);
}

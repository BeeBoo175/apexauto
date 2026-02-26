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

public interface IPasswordRecoveryRequestTransaction : IAsyncDisposable
{
    PasswordRecoveryRequestContext Context { get; }

    Task<IReadOnlyList<DateTimeOffset>> ReadRecentRequestsAsync(DateTimeOffset now, CancellationToken cancellationToken);

    Task CommitAsync(DateTimeOffset requestedAt, CancellationToken cancellationToken);
}

public interface IPasswordRecoveryStore
{
    Task<IPasswordRecoveryRequestTransaction> LockRequestAsync(string email, CancellationToken cancellationToken);

    Task<PasswordResetResult> ResetAsync(string token, string password, CancellationToken cancellationToken);
}

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

public interface IPasswordRecoveryStore
{
    Task RequestAsync(string email, CancellationToken cancellationToken);

    Task<PasswordResetResult> ResetAsync(string token, string password, CancellationToken cancellationToken);
}

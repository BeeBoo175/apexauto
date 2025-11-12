namespace Dealership.Application.Accounts;

public sealed class AuthenticationService(IAccountSessionStore sessions)
{
    public async Task<LoginSession?> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email?.Trim();

        if (request.LoginKey == Guid.Empty || string.IsNullOrEmpty(email) || email.Length > RegistrationService.EmailMaximumLength ||
            string.IsNullOrEmpty(request.Password) || request.Password.Length > RegistrationService.PasswordMaximumLength)
        {
            return null;
        }

        return await sessions.LoginAsync(request.LoginKey, email, request.Password, cancellationToken);
    }
}

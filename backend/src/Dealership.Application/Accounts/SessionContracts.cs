namespace Dealership.Application.Accounts;

public sealed record LoginRequest(Guid LoginKey, string? Email, string? Password);

public sealed record LoginSession(string AccessToken, DateTimeOffset ExpiresAt);

public sealed record CurrentActor(Guid UserId, string Role, Guid? BranchId);

public interface IAccountSessionStore
{
    Task<LoginSession?> LoginAsync(Guid loginKey, string email, string password, CancellationToken cancellationToken);

    Task<bool> IsActiveAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken);

    Task<CurrentActor?> GetActorAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken);

    Task RevokeAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken);
}

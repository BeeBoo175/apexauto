namespace Dealership.Infrastructure.Identity;

public sealed class AccountSession
{
    public Guid Id { get; init; }

    public Guid UserId { get; init; }

    // NULL once the retention executor has minimized the expired or revoked session.
    public string? SecurityStamp { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset ExpiresAt { get; init; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public DateTimeOffset? MinimizedAt { get; private set; }
}

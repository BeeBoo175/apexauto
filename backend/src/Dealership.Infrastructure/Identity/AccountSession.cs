namespace Dealership.Infrastructure.Identity;

public sealed class AccountSession
{
    public Guid Id { get; init; }

    public Guid UserId { get; init; }

    public string SecurityStamp { get; init; } = string.Empty;

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset ExpiresAt { get; init; }

    public DateTimeOffset? RevokedAt { get; private set; }
}

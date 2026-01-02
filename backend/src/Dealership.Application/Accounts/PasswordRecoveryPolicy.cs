namespace Dealership.Application.Accounts;

public static class PasswordRecoveryPolicy
{
    public const int LifetimeHours = 1;
    public const int MaximumRequestsPerWindow = 3;
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(LifetimeHours);
    public static readonly TimeSpan RequestCooldown = TimeSpan.FromMinutes(1);
    public static readonly TimeSpan RequestWindow = TimeSpan.FromHours(1);

    public static bool CanRequest(DateTimeOffset now, IReadOnlyList<DateTimeOffset> recentNewestFirst)
    {
        if (recentNewestFirst.Count >= MaximumRequestsPerWindow)
        {
            return false;
        }

        return recentNewestFirst.Count == 0 || recentNewestFirst[0] + RequestCooldown <= now;
    }
}

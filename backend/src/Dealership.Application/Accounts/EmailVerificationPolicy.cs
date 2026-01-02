namespace Dealership.Application.Accounts;

public static class EmailVerificationPolicy
{
    public const int LifetimeHours = 24;
    public const int MaximumResendsPerWindow = 3;
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(LifetimeHours);
    public static readonly TimeSpan ResendCooldown = TimeSpan.FromMinutes(1);
    public static readonly TimeSpan ResendWindow = TimeSpan.FromHours(1);

    public static TimeSpan? ResendDelay(DateTimeOffset now, IReadOnlyList<DateTimeOffset> recentNewestFirst)
    {
        if (recentNewestFirst.Count == 0)
        {
            return null;
        }

        if (recentNewestFirst[0] + ResendCooldown > now)
        {
            return recentNewestFirst[0] + ResendCooldown - now;
        }

        if (recentNewestFirst.Count >= MaximumResendsPerWindow)
        {
            return recentNewestFirst[^1] + ResendWindow - now;
        }

        return null;
    }
}

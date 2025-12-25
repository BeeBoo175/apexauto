namespace Dealership.Infrastructure.Mail;

public sealed record MailDeliveryClaim(Guid Id, DateTimeOffset ExpiresAt, int Attempts, DateTimeOffset RetryAt)
{
    public static MailDeliveryClaim Create(DateTimeOffset now, int previousAttempts)
    {
        var attempts = previousAttempts + 1;
        var retryDelay = TimeSpan.FromMinutes(Math.Min(30, Math.Pow(2, Math.Min(attempts - 1, 5))));
        return new MailDeliveryClaim(Guid.NewGuid(), now.AddSeconds(30), attempts, now + retryDelay);
    }
}

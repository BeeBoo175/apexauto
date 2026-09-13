using Dealership.Infrastructure.Persistence;

namespace Dealership.Api.Configuration;

public static class DatabasePrivilegeWarning
{
    private static readonly Action<ILogger, Exception?> BroadPrivileges = LoggerMessage.Define(LogLevel.Warning,
        new EventId(5, nameof(BroadPrivileges)),
        "The database role owns application tables or can DELETE or TRUNCATE them; erasure and retention guards " +
        "are only effective for a restricted runtime role (see the deployment runbook).");

    private static readonly Action<ILogger, string, Exception?> CheckFailed = LoggerMessage.Define<string>(
        LogLevel.Warning, new EventId(6, nameof(CheckFailed)),
        "The database privilege check could not run ({FailureType}).");

    // Free hosts often connect as the table owner, which makes restricted-role proofs say nothing about the
    // deployment. The check only warns; it never blocks start.
    public static async Task WarnAsync(WebApplication app)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            await using var scope = app.Services.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
            if (await RuntimePrivilegeCheck.HasBroadPrivilegesAsync(database, deadline.Token))
            {
                BroadPrivileges(app.Logger, null);
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            CheckFailed(app.Logger, exception.GetType().Name, null);
        }
    }
}

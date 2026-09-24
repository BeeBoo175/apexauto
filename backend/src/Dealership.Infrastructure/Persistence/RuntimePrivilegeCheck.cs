using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Dealership.Infrastructure.Persistence;

// Erasure and disposal rely on the runtime role being unable to DELETE or TRUNCATE retained evidence. A role that
// owns the tables or holds those privileges bypasses that boundary, so a deployment is warned at start.
public static class RuntimePrivilegeCheck
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
    public static async Task WarnAsync(IServiceProvider services, ILogger logger)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            await using var scope = services.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
            if (await HasBroadPrivilegesAsync(database, deadline.Token))
            {
                BroadPrivileges(logger, null);
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            CheckFailed(logger, exception.GetType().Name, null);
        }
    }
    public static Task<bool> HasBroadPrivilegesAsync(DealershipDbContext database, CancellationToken cancellationToken)
    {
        return database.Database.SqlQuery<bool>($"""
            SELECT EXISTS (
                SELECT 1 FROM pg_class c
                JOIN pg_namespace n ON n.oid = c.relnamespace
                WHERE n.nspname = 'public' AND c.relkind = 'r' AND
                    (pg_get_userbyid(c.relowner) = current_user OR
                     has_table_privilege(current_user, c.oid, 'DELETE') OR
                     has_table_privilege(current_user, c.oid, 'TRUNCATE'))) AS "Value"
            """).SingleAsync(cancellationToken);
    }
}

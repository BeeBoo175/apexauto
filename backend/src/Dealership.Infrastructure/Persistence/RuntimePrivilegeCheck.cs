using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Persistence;

// Erasure and disposal rely on the runtime role being unable to DELETE or TRUNCATE retained evidence. A role that
// owns the tables or holds those privileges bypasses that boundary, so a deployment is warned at start.
public static class RuntimePrivilegeCheck
{
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

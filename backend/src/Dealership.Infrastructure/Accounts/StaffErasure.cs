using Dealership.Domain.Staff;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Accounts;

// Every database change of an employee erasure, run by the caller inside its locked transaction. Work stays attributed
// to the retained user id; only the name, contact details and sign-in identity go.
internal static class StaffErasure
{
    public static async Task ApplyAsync(DealershipDbContext database, Guid userId, StaffProfile profile,
        Guid operationId, Guid actorId, string? reason, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await AccountErasure.CloseIdentityAsync(database, userId, now, cancellationToken);
        await database.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "StaffInvitations" SET "Email" = NULL, "Name" = NULL, "Mobile" = NULL
            WHERE "ActivatedUserId" = {userId} AND "Email" IS NOT NULL
            """, cancellationToken);
        profile.Erase(now);
        await AccountErasure.RecordAsync(database, userId, operationId, actorId, reason, now, cancellationToken);
    }
}

using Microsoft.EntityFrameworkCore;

namespace Dealership.IntegrationTests;

// Puts an account in the closed state exactly as the schema permits it: access ended and identity removed together.
internal static class ClosedOwnerSql
{
    internal static Task CloseAsync(ReservationDepositScenario scenario)
    {
        return scenario.Database.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "AspNetUsers" SET "ClosedAt" = now(), "IsActive" = false, "PasswordHash" = NULL,
                "EmailConfirmed" = false, "Email" = NULL, "NormalizedEmail" = NULL, "AccountUsername" = NULL,
                "UserName" = NULL, "NormalizedUserName" = NULL
            WHERE "Id" = {scenario.Data.OwnerId}
            """);
    }
}

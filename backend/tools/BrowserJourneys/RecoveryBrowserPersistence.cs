using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Dealership.Application.Accounts;
using Dealership.Infrastructure.Identity;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dealership.BrowserJourneys;

internal static class RecoveryBrowserPersistence
{
    public static async Task VerifyAsync(IServiceProvider services, HttpClient client,
        RecoveryBrowserAccount account, string scenario, string directory)
    {
        using var scope = services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        var challenge = await database.PasswordResetChallenges.AsNoTracking()
            .SingleAsync(item => item.UserId == account.UserId);

        if (challenge.DeliveredAt is null || challenge.ExpiresAt - challenge.CreatedAt != TimeSpan.FromHours(1))
        {
            throw new InvalidOperationException("Recovery browser mail lacks its persisted delivery or one-hour lifetime.");
        }

        if (scenario == "request")
        {
            var staff = await database.Users.AsNoTracking().SingleAsync(user => user.Id == account.UserId);

            if (staff.Role != AccountRoles.Administrator || staff.BranchId is not null || staff.IsActive || staff.EmailConfirmed ||
                await database.AccountSessions.AnyAsync(session => session.UserId == account.UserId))
            {
                throw new InvalidOperationException("Browser recovery changed disabled staff eligibility or assignment.");
            }
        }

        if (scenario != "reset")
        {
            if (challenge.ConsumedAt is not null || challenge.CompletedSecurityStamp is not null)
            {
                throw new InvalidOperationException("A recovery request or abandoned fragment changed credentials.");
            }

            File.WriteAllText(Path.Combine(directory, "persistence.json"), JsonSerializer.Serialize(new
            {
                delivered = true,
                consumed = false,
                challengeCount = await database.PasswordResetChallenges.CountAsync(item => item.UserId == account.UserId)
            }));
            return;
        }

        if (challenge.ConsumedAt is null || challenge.CompletedSecurityStamp is null)
        {
            throw new InvalidOperationException("The confirmed browser reset has no persisted receipt.");
        }

        var totalSessions = await database.AccountSessions.CountAsync(session => session.UserId == account.UserId);
        var revokedSessions = await database.AccountSessions.CountAsync(session =>
            session.UserId == account.UserId && session.RevokedAt != null);

        if (totalSessions != account.OldSessions.Length + 1 || revokedSessions != account.OldSessions.Length)
        {
            throw new InvalidOperationException("Browser reset did not durably revoke old sessions and preserve later sign-in.");
        }

        var before = await SnapshotAsync(database, account.UserId);
        var token = services.GetRequiredService<PasswordResetToken>().Create(challenge);
        using var replay = await client.PostAsJsonAsync("/api/auth/password-reset",
            new PasswordResetRequest(token, RecoveryBrowserFixture.RecoveredPassword));

        if (replay.StatusCode != HttpStatusCode.NoContent)
        {
            throw new InvalidOperationException("The exact browser reset receipt did not reconcile after API restart.");
        }

        var after = await SnapshotAsync(database, account.UserId);

        if (before != after)
        {
            throw new InvalidOperationException("A restarted exact reset replay changed credentials, sessions or receipt evidence.");
        }

        foreach (var oldSession in account.OldSessions)
        {
            await RequireAccountStatusAsync(client, oldSession, HttpStatusCode.Unauthorized);
        }

        await RequireAccountStatusAsync(client, account.UnrelatedSession!, HttpStatusCode.OK);
        var recoveredSession = File.ReadAllText(Path.Combine(directory, "recovered-session.txt"));
        await RequireAccountStatusAsync(client, recoveredSession, HttpStatusCode.OK);
        File.WriteAllText(Path.Combine(directory, "persistence.json"), JsonSerializer.Serialize(new
        {
            delivered = true,
            consumed = true,
            restartedExactReplay = true,
            oldSessionsRevoked = account.OldSessions.Length,
            unrelatedSessionSurvives = true,
            laterSessionSurvives = true,
            immutableReceipt = true
        }));
        Console.WriteLine("Restricted PostgreSQL and restarted API confirm reset receipts and isolated session revocation.");
    }

    private static async Task<string> SnapshotAsync(DealershipDbContext database, Guid userId)
    {
        var credentials = await database.Users.AsNoTracking().Where(user => user.Id == userId)
            .Select(user => new { user.PasswordHash, user.SecurityStamp, user.ConcurrencyStamp }).SingleAsync();
        var receipt = await database.PasswordResetChallenges.AsNoTracking().Where(item => item.UserId == userId)
            .Select(item => new { item.ConsumedAt, item.CompletedSecurityStamp }).SingleAsync();
        var sessions = await database.AccountSessions.AsNoTracking().Where(session => session.UserId == userId)
            .OrderBy(session => session.Id).Select(session => new { session.Id, session.RevokedAt }).ToArrayAsync();
        return JsonSerializer.Serialize(new { credentials, receipt, sessions });
    }

    private static async Task RequireAccountStatusAsync(HttpClient client, string token, HttpStatusCode expected)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/account");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await client.SendAsync(request);

        if (response.StatusCode != expected)
        {
            throw new InvalidOperationException("Restarted recovery session isolation returned an unexpected status.");
        }
    }
}

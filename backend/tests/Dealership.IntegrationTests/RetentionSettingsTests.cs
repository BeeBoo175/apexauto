using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dealership.Application.Accounts;
using Dealership.Domain.Accounts;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Dealership.IntegrationTests;

public sealed class RetentionSettingsTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    private const string Path = "/api/staff/settings/retention";

    [Fact]
    public async Task AdministratorsChangeRetentionWithVersionsConfirmationAndAReceipt()
    {
        using var factory = fixture.CreateFactory();
        using var admin = factory.CreateClient();
        await StaffInvitationTests.SignInAdministratorAsync(factory, admin);
        var adminId = (await admin.GetFromJsonAsync<AccountProfile>("/api/account/profile"))!.UserId;
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();

        // Nothing in a request path creates the record.
        var missing = await admin.GetAsync(Path);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.True(missing.Headers.CacheControl?.NoStore);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PutAsJsonAsync(Path, Update(30, 30, 30, 7, 1, false))).StatusCode);

        await database.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "RetentionSettings" ("Id", "SecurityArtifactDays", "MailDetailDays", "InvitationDays",
                "BusinessEvidenceYears", "UpdatedById", "UpdatedAtUtc", "Version")
            VALUES (1, 30, 30, 30, 7, {adminId}, now(), 1)
            """);
        var current = (await admin.GetFromJsonAsync<RetentionSettingsView>(Path))!;
        Assert.Equal((30, 30, 30, 7, 1L), (current.SecurityArtifactDays, current.MailDetailDays, current.InvitationDays,
            current.BusinessEvidenceYears, current.Version));
        Assert.Equal((7, 365, 1, 10), (current.Bounds.MinimumDays, current.Bounds.MaximumDays,
            current.Bounds.MinimumBusinessYears, current.Bounds.MaximumBusinessYears));

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync(Path, Update(6, 30, 30, 7, 1, false))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync(Path, Update(30, 30, 30, 11, 1, false))).StatusCode);

        // Extending needs no confirmation and writes one receipt row per changed value.
        var extended = await admin.PutAsJsonAsync(Path, Update(60, 30, 30, 7, 1, false));
        Assert.Equal(HttpStatusCode.OK, extended.StatusCode);
        Assert.Equal(2L, (await extended.Content.ReadFromJsonAsync<RetentionSettingsView>())!.Version);

        // A stale editor is refused, and shortening without the explicit confirmation is refused.
        Assert.Equal("settings-changed", await CodeAsync(await admin.PutAsJsonAsync(Path, Update(60, 30, 30, 7, 1, false))));
        var unconfirmed = await admin.PutAsJsonAsync(Path, Update(45, 30, 30, 7, 2, false));
        Assert.Equal(HttpStatusCode.Conflict, unconfirmed.StatusCode);
        Assert.Equal("shortening-confirmation-required", await CodeAsync(unconfirmed));
        Assert.Equal(60, (await admin.GetFromJsonAsync<RetentionSettingsView>(Path))!.SecurityArtifactDays);

        var shortened = await admin.PutAsJsonAsync(Path, Update(45, 30, 30, 5, 2, true));
        Assert.Equal(HttpStatusCode.OK, shortened.StatusCode);

        var changes = await database.RetentionSettingChanges.AsNoTracking().OrderBy(item => item.SettingsVersion)
            .ThenBy(item => item.Setting).ToListAsync();
        Assert.Equal(
        [
            (2L, "SecurityArtifactDays", 30, 60, false),
            (3L, "BusinessEvidenceYears", 7, 5, true),
            (3L, "SecurityArtifactDays", 60, 45, true)
        ], changes.Select(item => (item.SettingsVersion, item.Setting, item.PreviousValue, item.NewValue,
            item.ShorteningConfirmed)));
        Assert.All(changes, change => Assert.Equal(adminId, change.ActorId));

        // The receipt is append-only for the runtime role.
        Assert.Equal(PostgresErrorCodes.CheckViolation, await FailureAsync(database,
            "UPDATE \"RetentionSettingChanges\" SET \"NewValue\" = 99"));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, await FailureAsync(database,
            "DELETE FROM \"RetentionSettingChanges\""));
    }

    [Fact]
    public async Task OnlyAdministratorsReadOrChangeRetention()
    {
        using var factory = fixture.CreateFactory();
        using var customer = factory.CreateClient();
        await IntakePhotoFixture.RegisterAsync(customer);

        var denied = await customer.GetAsync(Path);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.True(denied.Headers.CacheControl?.NoStore);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.PutAsJsonAsync(Path, Update(30, 30, 30, 7, 1, false))).StatusCode);
        using var visitor = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await visitor.GetAsync(Path)).StatusCode);
    }

    private static RetentionSettingsUpdateRequest Update(int security, int mail, int invitations, int years, long version,
        bool confirmed) => new(security, mail, invitations, years, version, confirmed);

    private static async Task<string?> CodeAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return body.RootElement.GetProperty("code").GetString();
    }

    private static async Task<string> FailureAsync(DealershipDbContext database, string sql)
    {
        var exception = await Assert.ThrowsAnyAsync<Exception>(() => database.Database.ExecuteSqlRawAsync(sql));
        var postgres = exception as PostgresException ?? exception.InnerException as PostgresException;
        return Assert.IsType<PostgresException>(postgres).SqlState;
    }
}

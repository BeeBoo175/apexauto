using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Dealership.Application.Accounts;
using Dealership.Application.Branches;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dealership.IntegrationTests;

// Retention settings and preservation holds under careless, concurrent and unauthorized use.
public sealed class AdversarialRetentionSettingsTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    private const string Path = "/api/staff/settings/retention";

    [Fact]
    public async Task OutOfBoundsNonNumericAndNegativeValuesNeverChangeTheSettings()
    {
        // Guarantee: only whole numbers inside the approved bounds are stored, and every refusal leaves version and
        // receipts untouched; the exact bounds themselves are accepted (control).
        using var factory = fixture.CreateFactory();
        using var admin = factory.CreateClient();
        await StaffInvitationTests.SignInAdministratorAsync(factory, admin);
        await SeedAsync(factory, admin);

        var hostile = new[]
        {
            Body("0", "30", "30", "7", "1"), Body("-1", "30", "30", "7", "1"), Body("6", "30", "30", "7", "1"),
            Body("366", "30", "30", "7", "1"), Body("2147483647", "30", "30", "7", "1"),
            Body("30", "30", "30", "0", "1"), Body("30", "30", "30", "11", "1"), Body("30", "30", "30", "-7", "1"),
            Body("30", "-30", "30", "7", "1"), Body("30", "30", "999999999999", "7", "1"),
            Body("\"thirty\"", "30", "30", "7", "1"), Body("null", "30", "30", "7", "1"), Body("30.5", "30", "30", "7", "1"),
            Body("true", "30", "30", "7", "1"), Body("[30]", "30", "30", "7", "1"),
            Body("1e2", "30", "30", "7", "1"), Body("30", "30", "30", "7", "-1"),
            Body("30", "30", "30", "7", "null"), "{}", "[]", "null", "{\"securityArtifactDays\":30,", "\"just text\""
        };

        foreach (var body in hostile)
        {
            var response = await admin.PutAsync(Path, new StringContent(body, new MediaTypeHeaderValue("application/json")));
            Assert.True((int)response.StatusCode is >= 400 and < 500, $"{body} answered {(int)response.StatusCode}");
        }

        var unchanged = (await admin.GetFromJsonAsync<RetentionSettingsView>(Path))!;
        Assert.Equal((30, 30, 30, 7, 1L), (unchanged.SecurityArtifactDays, unchanged.MailDetailDays,
            unchanged.InvitationDays, unchanged.BusinessEvidenceYears, unchanged.Version));
        Assert.Equal(0, await ChangeCountAsync(factory));

        // The bounds are inclusive: the smallest and largest approved values are accepted.
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsync(Path, Json(Body("7", "7", "7", "1", "1", true)))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsync(Path, Json(Body("365", "365", "365", "10", "2")))).StatusCode);
    }

    [Fact]
    public async Task ANoOpSaveAndAnUnknownFieldAreHandledWithoutServerErrorsOrReceipts()
    {
        // Guarantee: saving the values already stored is not a failure and never writes a receipt.
        using var factory = fixture.CreateFactory();
        using var admin = factory.CreateClient();
        await StaffInvitationTests.SignInAdministratorAsync(factory, admin);
        await SeedAsync(factory, admin);

        var same = await admin.PutAsync(Path, Json(Body("30", "30", "30", "7", "1")));
        Assert.True((int)same.StatusCode < 500, ((int)same.StatusCode).ToString(System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(0, await ChangeCountAsync(factory));

        var withUnknown = await admin.PutAsync(Path, Json(
            "{\"securityArtifactDays\":31,\"mailDetailDays\":30,\"invitationDays\":30,\"businessEvidenceYears\":7," +
            "\"expectedVersion\":1,\"shorteningConfirmed\":false,\"updatedById\":\"" + Guid.NewGuid() + "\",\"version\":99}"));
        Assert.True((int)withUnknown.StatusCode < 500, ((int)withUnknown.StatusCode).ToString(System.Globalization.CultureInfo.InvariantCulture));
        var current = (await admin.GetFromJsonAsync<RetentionSettingsView>(Path))!;
        // Whatever the answer, the client cannot choose the version or the actor recorded for the change.
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        var adminId = (await admin.GetFromJsonAsync<AccountProfile>("/api/account/profile"))!.UserId;
        Assert.All(await database.RetentionSettingChanges.AsNoTracking().ToListAsync(),
            change => Assert.Equal(adminId, change.ActorId));
        Assert.True(current.Version is 1 or 2);
    }

    [Fact]
    public async Task ShorteningWithoutConfirmationIsRefusedForEveryValueAndAShortenedSettingIsNeverSilentlyApplied()
    {
        // Guarantee: reducing any one of the four values needs the explicit confirmation, even alongside an extension.
        using var factory = fixture.CreateFactory();
        using var admin = factory.CreateClient();
        await StaffInvitationTests.SignInAdministratorAsync(factory, admin);
        await SeedAsync(factory, admin);

        foreach (var body in new[]
        {
            Body("29", "30", "30", "7", "1"), Body("30", "29", "30", "7", "1"), Body("30", "30", "29", "7", "1"),
            Body("30", "30", "30", "6", "1"),
            // An extension of one value cannot smuggle a shortening of another.
            Body("60", "30", "30", "6", "1"), Body("30", "30", "60", "6", "1")
        })
        {
            var response = await admin.PutAsync(Path, Json(body));
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            Assert.Equal("shortening-confirmation-required", document.RootElement.GetProperty("code").GetString());
        }

        var current = (await admin.GetFromJsonAsync<RetentionSettingsView>(Path))!;
        Assert.Equal((30, 30, 30, 7, 1L), (current.SecurityArtifactDays, current.MailDetailDays, current.InvitationDays,
            current.BusinessEvidenceYears, current.Version));
        // Control: the same shortening with the confirmation is applied and receipted as confirmed.
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsync(Path, Json(Body("29", "30", "30", "7", "1", true)))).StatusCode);
        using var scope = factory.Services.CreateScope();
        var change = await scope.ServiceProvider.GetRequiredService<DealershipDbContext>().RetentionSettingChanges
            .AsNoTracking().SingleAsync();
        Assert.True(change.ShorteningConfirmed);
    }

    [Fact]
    public async Task TwoAdministratorsSavingTheSameVersionAtOnceProduceExactlyOneChange()
    {
        // Guarantee: concurrent saves serialize on the settings row; the loser is told the settings changed and
        // never overwrites the winner.
        using var factory = fixture.CreateFactory();
        using var first = factory.CreateClient();
        using var second = factory.CreateClient();
        await StaffInvitationTests.SignInAdministratorAsync(factory, first);
        await StaffInvitationTests.SignInAdministratorAsync(factory, second);
        await SeedAsync(factory, first);

        for (var round = 0; round < 6; round++)
        {
            var version = (await first.GetFromJsonAsync<RetentionSettingsView>(Path))!.Version;
            var firstSave = first.PutAsync(Path, Json(Body((40 + round * 2).ToString(System.Globalization.CultureInfo.InvariantCulture), "30", "30", "7", version.ToString(System.Globalization.CultureInfo.InvariantCulture), true)));
            var secondSave = second.PutAsync(Path, Json(Body((41 + round * 2).ToString(System.Globalization.CultureInfo.InvariantCulture), "30", "30", "7", version.ToString(System.Globalization.CultureInfo.InvariantCulture), true)));
            var results = await Task.WhenAll(firstSave, secondSave);

            Assert.Single(results, item => item.StatusCode == HttpStatusCode.OK);
            Assert.Single(results, item => item.StatusCode == HttpStatusCode.Conflict);
            Assert.Equal(version + 1, (await first.GetFromJsonAsync<RetentionSettingsView>(Path))!.Version);
        }

        Assert.Equal(6, await ChangeCountAsync(factory));
    }

    [Fact]
    public async Task EveryNonAdministratorRoleIsRefusedForSettingsAndHolds()
    {
        // Guarantee: no employee role other than Administrator can read or change retention settings or place a hold.
        using var factory = fixture.CreateFactory();
        using var admin = factory.CreateClient();
        await StaffInvitationTests.SignInAdministratorAsync(factory, admin);
        await SeedAsync(factory, admin);
        var branchId = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<BranchProvisioningService>().ProvisionBranchAsync(
                new(branchId, $"ApexAuto {branchId:N}", "12 Jalan Pengaturcara", "Shah Alam", "Selangor", "+60355651234", "GLN"),
                CancellationToken.None);
        }

        var hold = Json("{\"reservationNumber\":\"RSV-2026-000001\",\"until\":\"2099-01-01T00:00:00Z\",\"reason\":\"Dispute\"}");
        foreach (var role in new[] { AccountRoles.SalesAgent, AccountRoles.WorkshopTechnician, AccountRoles.BranchManager,
            AccountRoles.GeneralManager })
        {
            var email = $"{role.ToLowerInvariant()}.{Guid.NewGuid():N}@example.invalid";
            using (var scope = factory.Services.CreateScope())
            {
                var groupScoped = role == AccountRoles.GeneralManager;
                Assert.True((await scope.ServiceProvider.GetRequiredService<StaffProvisioningService>().ProvisionStaffAsync(
                    new(Guid.NewGuid(), email, "Password123!", role, groupScoped ? null : branchId, "Hana Ismail",
                        "0123456781"), CancellationToken.None)).Created);
            }

            using var client = factory.CreateClient();
            var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(Guid.NewGuid(), email, "Password123!"));
            var session = (await login.Content.ReadFromJsonAsync<LoginSession>())!;
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);

            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(Path)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsync(Path, Json(Body("31", "30", "30", "7", "1")))).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/staff/retention-holds")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsync("/api/staff/retention-holds", hold)).StatusCode);
        }

        Assert.Equal(1L, (await admin.GetFromJsonAsync<RetentionSettingsView>(Path))!.Version);
    }

    private static string Body(string security, string mail, string invitations, string years, string version,
        bool confirmed = false) =>
        "{\"securityArtifactDays\":" + security + ",\"mailDetailDays\":" + mail + ",\"invitationDays\":" + invitations +
        ",\"businessEvidenceYears\":" + years + ",\"expectedVersion\":" + version + ",\"shorteningConfirmed\":" +
        (confirmed ? "true" : "false") + "}";

    private static StringContent Json(string body) => new(body, Encoding.UTF8, "application/json");

    // The class shares one database, so each scenario resets the singleton and its receipts through the owner role.
    private async Task SeedAsync(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory,
        HttpClient admin)
    {
        var adminId = (await admin.GetFromJsonAsync<AccountProfile>("/api/account/profile"))!.UserId;
        await using var owner = new Npgsql.NpgsqlConnection(fixture.Database.ConnectionString);
        await owner.OpenAsync();
        foreach (var sql in new[]
        {
            "ALTER TABLE \"RetentionSettingChanges\" DISABLE TRIGGER USER",
            "ALTER TABLE \"RetentionSettings\" DISABLE TRIGGER USER",
            "DELETE FROM \"RetentionSettingChanges\"",
            "DELETE FROM \"RetentionSettings\"",
            $"INSERT INTO \"RetentionSettings\" (\"Id\", \"SecurityArtifactDays\", \"MailDetailDays\", \"InvitationDays\", " +
            $"\"BusinessEvidenceYears\", \"UpdatedById\", \"UpdatedAtUtc\", \"Version\") VALUES (1, 30, 30, 30, 7, '{adminId}', now(), 1)",
            "ALTER TABLE \"RetentionSettingChanges\" ENABLE TRIGGER USER",
            "ALTER TABLE \"RetentionSettings\" ENABLE TRIGGER USER"
        })
        {
            await RetentionMinimizationTests.ExecuteAsync(owner, sql);
        }
    }

    private static async Task<int> ChangeCountAsync(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<DealershipDbContext>().RetentionSettingChanges
            .AsNoTracking().CountAsync();
    }
}

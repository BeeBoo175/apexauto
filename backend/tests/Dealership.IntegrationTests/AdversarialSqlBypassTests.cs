using Dealership.Application.Accounts;
using Dealership.Domain.Accounts;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using static Dealership.IntegrationTests.RetentionSql;

namespace Dealership.IntegrationTests;

// Direct SQL as the restricted runtime role against erased accounts and the tables that keep retained history.
// Each probe must be refused by the database and must leave the erased state exactly as it was.
public sealed class AdversarialSqlBypassTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    private static readonly string[] HistoryTables =
    [
        "AccountEvents", "AccountSessions", "AspNetUsers", "CustomerProfiles", "StaffProfiles", "StaffInvitations",
        "EmailVerificationChallenges", "PasswordResetChallenges", "Reservations", "ReservationRefunds",
        "ReservationDepositPayments", "SalesOrders", "SalesOrderEvents", "SaleDocuments", "SaleDocumentCorrections",
        "AppointmentCheckIns", "IntakeAppointments", "RetentionSettings", "RetentionSettingChanges", "PhotoRemovals",
        "InspectionPhotoRemovals", "AccountPictureRemovals", "ListingPhotoRemovals"
    ];

    [Fact]
    public async Task TheRuntimeRoleCannotDeleteTruncateDisableTriggersOrRedefineProtections()
    {
        // Guarantee: the runtime role owns no escape hatch: no DELETE, TRUNCATE, trigger control, replication-role
        // switch or DDL against retained history. SELECT works, so the refusals are about privilege, not a missing table.
        foreach (var table in HistoryTables)
        {
            await ExpectAsync($"SELECT count(*) FROM \"{table}\"");
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, await FailureAsync($"DELETE FROM \"{table}\""));
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, await FailureAsync($"TRUNCATE \"{table}\""));
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege,
                await FailureAsync($"ALTER TABLE \"{table}\" DISABLE TRIGGER USER"));
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege,
                await FailureAsync($"ALTER TABLE \"{table}\" DISABLE TRIGGER ALL"));
        }

        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, await FailureAsync("SET session_replication_role = replica"));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege,
            await FailureAsync("ALTER TABLE \"AspNetUsers\" DROP CONSTRAINT \"CK_Users_Closed\""));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege,
            await FailureAsync("DROP TRIGGER account_events_no_delete ON \"AccountEvents\""));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, await FailureAsync(
            "CREATE OR REPLACE FUNCTION public.forbid_account_event_change() RETURNS trigger LANGUAGE plpgsql AS $$ " +
            "BEGIN RETURN NEW; END $$"));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, await FailureAsync("CREATE TABLE public.shadow (id int)"));
    }

    [Fact]
    public async Task AnErasedCustomerCannotBeReopenedOrHaveItsDetailsRestoredByDirectSql()
    {
        // Guarantee: erasure markers and removed columns cannot be cleared or refilled by a statement from the runtime role.
        using var world = AdversarialWorld.Create(fixture);
        var admin = await world.AdministratorAsync();
        var (customerId, registration) = await world.CustomerAsync();
        Assert.Equal(AccountErasureStatus.Erased,
            (await world.EraseCustomerAsync(admin, customerId, Guid.NewGuid(), AdversarialWorld.AdministratorPassword)).Status);
        var before = await SnapshotAsync(customerId);

        const string closed = "CK_Users_Closed";
        const string customerErased = "CK_CustomerProfiles_Erased";
        const string receiptImmutable = "CK_AccountEvents_Immutable";
        var probes = new (string Sql, string Guard)[]
        {
            ($"UPDATE \"AspNetUsers\" SET \"ClosedAt\" = NULL WHERE \"Id\" = '{customerId}'", closed),
            ($"UPDATE \"AspNetUsers\" SET \"ClosedAt\" = \"ClosedAt\" + interval '1 day' WHERE \"Id\" = '{customerId}'", closed),
            ($"UPDATE \"AspNetUsers\" SET \"IsActive\" = true WHERE \"Id\" = '{customerId}'", closed),
            ($"UPDATE \"AspNetUsers\" SET \"PasswordHash\" = 'AQAAAAEAACcQAAAA' WHERE \"Id\" = '{customerId}'", closed),
            ($"UPDATE \"AspNetUsers\" SET \"Email\" = '{registration.Email}' WHERE \"Id\" = '{customerId}'", closed),
            ($"UPDATE \"AspNetUsers\" SET \"Email\" = '{registration.Email}', " +
                $"\"NormalizedEmail\" = '{registration.Email!.ToUpperInvariant()}' WHERE \"Id\" = '{customerId}'", closed),
            // The username fits its 30-character column, so the closed-account guard is what refuses the statement.
            ($"UPDATE \"AspNetUsers\" SET \"ClosedAt\" = NULL, \"IsActive\" = true, " +
                $"\"Email\" = '{registration.Email}', \"NormalizedEmail\" = '{registration.Email.ToUpperInvariant()}', " +
                $"\"UserName\" = '{registration.Email}', \"NormalizedUserName\" = '{registration.Email.ToUpperInvariant()}', " +
                $"\"AccountUsername\" = 'revived.{Guid.NewGuid().ToString("N")[..12]}', \"PasswordHash\" = 'AQAAAAEAACcQAAAA' " +
                $"WHERE \"Id\" = '{customerId}'", closed),
            ($"UPDATE \"CustomerProfiles\" SET \"ErasedAt\" = NULL WHERE \"UserId\" = '{customerId}'", customerErased),
            ($"UPDATE \"CustomerProfiles\" SET \"Nric\" = '{registration.Nric}' WHERE \"UserId\" = '{customerId}'", customerErased),
            ($"UPDATE \"CustomerProfiles\" SET \"ErasedAt\" = NULL, \"Nric\" = '{registration.Nric}', " +
                $"\"LegalName\" = '{registration.LegalName}', \"Mobile\" = '0123456789', " +
                $"\"ResidentialAddress\" = '18 Jalan Melati, Petaling Jaya' WHERE \"UserId\" = '{customerId}'", customerErased),
            ($"UPDATE \"AccountEvents\" SET \"Reason\" = 'rewritten' WHERE \"TargetId\" = '{customerId}'", receiptImmutable),
            ($"UPDATE \"AccountEvents\" SET \"MinimizedAt\" = NULL WHERE \"TargetId\" = '{customerId}'", receiptImmutable),
            ($"UPDATE \"AccountEvents\" SET \"ActorId\" = '{customerId}' WHERE \"TargetId\" = '{customerId}'", receiptImmutable),
            ($"UPDATE \"AccountEvents\" SET \"TargetId\" = '{admin}' WHERE \"TargetId\" = '{customerId}'", receiptImmutable),
            ($"UPDATE \"AccountEvents\" SET \"Kind\" = 'Suspended' WHERE \"TargetId\" = '{customerId}'", receiptImmutable)
        };

        foreach (var (sql, guard) in probes)
        {
            await ExpectRuntimeRefusalAsync(fixture.Database.RuntimeConnectionString, sql,
                PostgresErrorCodes.CheckViolation, guard);
            Assert.Equal(before, await SnapshotAsync(customerId));
        }

        // The runtime role has no DELETE privilege on the receipts at all.
        await ExpectRuntimeRefusalAsync(fixture.Database.RuntimeConnectionString,
            $"DELETE FROM \"AccountEvents\" WHERE \"TargetId\" = '{customerId}'",
            PostgresErrorCodes.InsufficientPrivilege, "permission denied for table AccountEvents");
        Assert.Equal(before, await SnapshotAsync(customerId));
    }

    [Fact]
    public async Task AnErasedEmployeeCannotBeReopenedOrHaveNamesRestoredByDirectSql()
    {
        // Guarantee: the same holds for an erased employee, and nothing can reassign the erasure receipt.
        using var world = AdversarialWorld.Create(fixture);
        var admin = await world.AdministratorAsync();
        var peer = await world.AdministratorAsync();
        Assert.Equal(AccountErasureStatus.Erased,
            (await world.EraseStaffAsync(admin, peer, Guid.NewGuid(), AdversarialWorld.AdministratorPassword)).Status);
        var before = await SnapshotAsync(peer);

        var probes = new (string Sql, string Guard)[]
        {
            ($"UPDATE \"StaffProfiles\" SET \"ErasedAt\" = NULL WHERE \"UserId\" = '{peer}'", "CK_StaffProfiles_Erased"),
            ($"UPDATE \"StaffProfiles\" SET \"Name\" = 'Amir Hakim' WHERE \"UserId\" = '{peer}'", "CK_StaffProfiles_Erased"),
            ($"UPDATE \"StaffProfiles\" SET \"ErasedAt\" = NULL, \"Name\" = 'Amir Hakim', \"Mobile\" = '0123456789' WHERE \"UserId\" = '{peer}'",
                "CK_StaffProfiles_Erased"),
            ($"UPDATE \"AspNetUsers\" SET \"ClosedAt\" = NULL, \"IsActive\" = true WHERE \"Id\" = '{peer}'", "CK_Users_Closed"),
            ($"UPDATE \"AccountEvents\" SET \"TargetId\" = '{admin}' WHERE \"TargetId\" = '{peer}'", "CK_AccountEvents_Immutable")
        };

        foreach (var (sql, guard) in probes)
        {
            await ExpectRuntimeRefusalAsync(fixture.Database.RuntimeConnectionString, sql,
                PostgresErrorCodes.CheckViolation, guard);
            Assert.Equal(before, await SnapshotAsync(peer));
        }
    }

    [Fact]
    public async Task ARuntimeRoleCannotRecordAnErasureReceiptForAnOpenAccountOrMinimizeItsAudit()
    {
        // Guarantee: an erasure receipt and audit minimization are only possible for an account that is already closed.
        using var world = AdversarialWorld.Create(fixture);
        var admin = await world.AdministratorAsync();
        var (openId, _) = await world.CustomerAsync();
        // An ordinary audit row for the open customer, written through the production command.
        await world.RunAsync(async services =>
        {
            await services.GetRequiredService<DealershipDbContext>().Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "AccountEvents" ("Id", "TargetId", "ActorId", "Kind", "Reason", "RecordedAt")
                VALUES ({Guid.NewGuid()}, {openId}, {admin}, 'NricRevealed', 'Support check', now())
                """);
            return true;
        });

        var runtime = fixture.Database.RuntimeConnectionString;
        await ExpectRuntimeRefusalAsync(runtime,
            $"UPDATE \"AccountEvents\" SET \"Reason\" = NULL, \"MinimizedAt\" = now() WHERE \"TargetId\" = '{openId}'",
            PostgresErrorCodes.CheckViolation, "CK_AccountEvents_Immutable");
        await ExpectRuntimeRefusalAsync(runtime,
            $"UPDATE \"AccountEvents\" SET \"Reason\" = 'rewritten' WHERE \"TargetId\" = '{openId}'",
            PostgresErrorCodes.CheckViolation, "CK_AccountEvents_Immutable");
        await ExpectRuntimeRefusalAsync(runtime,
            $"INSERT INTO \"AccountEvents\" (\"Id\", \"TargetId\", \"ActorId\", \"Kind\", \"RecordedAt\", \"MinimizedAt\") " +
            $"VALUES ('{Guid.NewGuid()}', '{openId}', '{admin}', 'Erased', now(), now())",
            PostgresErrorCodes.CheckViolation, "CK_AccountEvents_ErasedTarget");
        Assert.True(await world.IsOpenAsync(openId));
        Assert.Equal(0, await world.ErasedEventCountAsync(openId));
    }

    private async Task<string> SnapshotAsync(Guid id)
    {
        await using var connection = new NpgsqlConnection(fixture.Database.RuntimeConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"""
            SELECT concat_ws('|', u."ClosedAt", u."IsActive", u."Email", u."NormalizedEmail", u."UserName",
                u."AccountUsername", u."PasswordHash", u."EmailConfirmed", u."SecurityStamp",
                c."ErasedAt", c."LegalName", c."Nric", c."Mobile", c."ResidentialAddress",
                s."ErasedAt", s."Name", s."Mobile",
                (SELECT string_agg(concat_ws(',', e."Id", e."Kind", e."ActorId", e."TargetId", e."Reason", e."MinimizedAt"), ';'
                    ORDER BY e."Id") FROM "AccountEvents" e WHERE e."TargetId" = u."Id"))
            FROM "AspNetUsers" u
            LEFT JOIN "CustomerProfiles" c ON c."UserId" = u."Id"
            LEFT JOIN "StaffProfiles" s ON s."UserId" = u."Id"
            WHERE u."Id" = '{id}'
            """, connection);
        return (string)(await command.ExecuteScalarAsync())!;
    }

    private async Task ExpectAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(fixture.Database.RuntimeConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteScalarAsync();
    }

    // The SQL state of the refusal; a statement the database accepted, or an UPDATE or DELETE that matched no row, fails the test.
    private async Task<string> FailureAsync(string sql) =>
        (await RuntimeRefusalAsync(fixture.Database.RuntimeConnectionString, sql)).SqlState;
}

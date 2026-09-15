using Dealership.Application.Accounts;
using Dealership.Domain.Accounts;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

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

        var probes = new (string Name, string Sql)[]
        {
            ("clear the closed marker", $"UPDATE \"AspNetUsers\" SET \"ClosedAt\" = NULL WHERE \"Id\" = '{customerId}'"),
            ("move the closed marker", $"UPDATE \"AspNetUsers\" SET \"ClosedAt\" = \"ClosedAt\" + interval '1 day' WHERE \"Id\" = '{customerId}'"),
            ("reactivate", $"UPDATE \"AspNetUsers\" SET \"IsActive\" = true WHERE \"Id\" = '{customerId}'"),
            ("restore a password", $"UPDATE \"AspNetUsers\" SET \"PasswordHash\" = 'AQAAAAEAACcQAAAA' WHERE \"Id\" = '{customerId}'"),
            ("restore the email", $"UPDATE \"AspNetUsers\" SET \"Email\" = '{registration.Email}' WHERE \"Id\" = '{customerId}'"),
            ("restore the email and its normal form", $"UPDATE \"AspNetUsers\" SET \"Email\" = '{registration.Email}', " +
                $"\"NormalizedEmail\" = '{registration.Email!.ToUpperInvariant()}' WHERE \"Id\" = '{customerId}'"),
            ("reopen the whole account in one statement", $"UPDATE \"AspNetUsers\" SET \"ClosedAt\" = NULL, \"IsActive\" = true, " +
                $"\"Email\" = '{registration.Email}', \"NormalizedEmail\" = '{registration.Email.ToUpperInvariant()}', " +
                $"\"UserName\" = '{registration.Email}', \"NormalizedUserName\" = '{registration.Email.ToUpperInvariant()}', " +
                $"\"AccountUsername\" = 'revived.{Guid.NewGuid():N}', \"PasswordHash\" = 'AQAAAAEAACcQAAAA' WHERE \"Id\" = '{customerId}'"),
            ("clear the erased marker", $"UPDATE \"CustomerProfiles\" SET \"ErasedAt\" = NULL WHERE \"UserId\" = '{customerId}'"),
            ("restore the NRIC", $"UPDATE \"CustomerProfiles\" SET \"Nric\" = '{registration.Nric}' WHERE \"UserId\" = '{customerId}'"),
            ("restore every profile detail", $"UPDATE \"CustomerProfiles\" SET \"ErasedAt\" = NULL, \"Nric\" = '{registration.Nric}', " +
                $"\"LegalName\" = '{registration.LegalName}', \"Mobile\" = '0123456789', " +
                $"\"ResidentialAddress\" = '18 Jalan Melati, Petaling Jaya' WHERE \"UserId\" = '{customerId}'"),
            ("rewrite the receipt reason", $"UPDATE \"AccountEvents\" SET \"Reason\" = 'rewritten' WHERE \"TargetId\" = '{customerId}'"),
            ("clear the receipt marker", $"UPDATE \"AccountEvents\" SET \"MinimizedAt\" = NULL WHERE \"TargetId\" = '{customerId}'"),
            ("change the receipt actor", $"UPDATE \"AccountEvents\" SET \"ActorId\" = '{customerId}' WHERE \"TargetId\" = '{customerId}'"),
            ("change the receipt target", $"UPDATE \"AccountEvents\" SET \"TargetId\" = '{admin}' WHERE \"TargetId\" = '{customerId}'"),
            ("change the receipt kind", $"UPDATE \"AccountEvents\" SET \"Kind\" = 'Suspended' WHERE \"TargetId\" = '{customerId}'"),
            ("delete the receipt", $"DELETE FROM \"AccountEvents\" WHERE \"TargetId\" = '{customerId}'")
        };

        foreach (var (name, sql) in probes)
        {
            var state = await FailureOrNullAsync(sql);
            Assert.True(state is not null, $"The database accepted: {name}");
            Assert.Equal(before, await SnapshotAsync(customerId));
        }
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

        var probes = new[]
        {
            $"UPDATE \"StaffProfiles\" SET \"ErasedAt\" = NULL WHERE \"UserId\" = '{peer}'",
            $"UPDATE \"StaffProfiles\" SET \"Name\" = 'Amir Hakim' WHERE \"UserId\" = '{peer}'",
            $"UPDATE \"StaffProfiles\" SET \"ErasedAt\" = NULL, \"Name\" = 'Amir Hakim', \"Mobile\" = '0123456789' WHERE \"UserId\" = '{peer}'",
            $"UPDATE \"AspNetUsers\" SET \"ClosedAt\" = NULL, \"IsActive\" = true WHERE \"Id\" = '{peer}'",
            $"UPDATE \"AccountEvents\" SET \"TargetId\" = '{admin}' WHERE \"TargetId\" = '{peer}'"
        };

        foreach (var sql in probes)
        {
            Assert.True(await FailureOrNullAsync(sql) is not null, $"The database accepted: {sql}");
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

        Assert.NotNull(await FailureOrNullAsync(
            $"UPDATE \"AccountEvents\" SET \"Reason\" = NULL, \"MinimizedAt\" = now() WHERE \"TargetId\" = '{openId}'"));
        Assert.NotNull(await FailureOrNullAsync(
            $"UPDATE \"AccountEvents\" SET \"Reason\" = 'rewritten' WHERE \"TargetId\" = '{openId}'"));
        Assert.NotNull(await FailureOrNullAsync(
            $"INSERT INTO \"AccountEvents\" (\"Id\", \"TargetId\", \"ActorId\", \"Kind\", \"RecordedAt\", \"MinimizedAt\") " +
            $"VALUES ('{Guid.NewGuid()}', '{openId}', '{admin}', 'Erased', now(), now())"));
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

    private async Task<string> FailureAsync(string sql)
    {
        var state = await FailureOrNullAsync(sql);
        Assert.NotNull(state);
        return state;
    }

    // The SQL state of the refusal, or null when the database accepted the statement.
    private async Task<string?> FailureOrNullAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(fixture.Database.RuntimeConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        try
        {
            var affected = await command.ExecuteNonQueryAsync();
            // An UPDATE that matched no row changed nothing, so it is not a bypass.
            return sql.StartsWith("UPDATE", StringComparison.Ordinal) && affected == 0 ? "no-rows" : null;
        }
        catch (PostgresException failure)
        {
            return failure.SqlState;
        }
    }
}

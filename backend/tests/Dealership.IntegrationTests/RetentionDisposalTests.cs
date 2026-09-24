using Dealership.Application.Intake;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using static Dealership.IntegrationTests.RetentionMinimizationTests;
using static Dealership.IntegrationTests.RetentionSql;

namespace Dealership.IntegrationTests;

// Its own class: the check-in scenario provisions a branch with a name that is unique per class database.
public sealed class RetentionDisposalTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task CheckInNotesAreRemovedOnlyAfterTheYearsInForceHaveRun()
    {
        using var data = await CheckInTestData.CreateAsync(fixture);
        var appointment = await data.BookAsync();
        await data.ClaimAsync(appointment.Id);
        using (var scope = data.Factory.Services.CreateScope())
        {
            var result = await scope.ServiceProvider.GetRequiredService<AppointmentCheckInService>()
                .CheckInAsync(data.StaffId, appointment.Id, CheckInTestData.Evidence, CancellationToken.None);
            Assert.Equal(CheckInStatus.Succeeded, result.Status);
        }

        using var sqlScope = data.Factory.Services.CreateScope();
        var database = sqlScope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        Assert.Equal(PostgresErrorCodes.CheckViolation, await FailureAsync(database,
            "UPDATE \"AppointmentCheckIns\" SET \"Notes\" = 'Changed'"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, await FailureAsync(database,
            "UPDATE \"AppointmentCheckIns\" SET \"Notes\" = NULL, \"MileageKilometres\" = \"MileageKilometres\" + 1"));

        // The owner role ages the receipt, because the runtime role cannot rewrite its time.
        await using var owner = new NpgsqlConnection(fixture.Database.ConnectionString);
        await owner.OpenAsync();
        await AgeCheckInAsync(owner, "6 years");
        await Worker(data.Factory).RunBatchAsync(CancellationToken.None);
        Assert.NotNull(await NotesAsync(database));

        // An extended period reaches records that are already old; the default seven years does not yet apply.
        await AgeCheckInAsync(owner, "8 years");
        await ExecuteAsync(owner, $"""
            INSERT INTO "RetentionSettings" ("Id", "SecurityArtifactDays", "MailDetailDays", "InvitationDays",
                "BusinessEvidenceYears", "UpdatedById", "UpdatedAtUtc", "Version")
            VALUES (1, 30, 30, 30, 10, '{data.StaffId}', now(), 1)
            """);
        await Worker(data.Factory).RunBatchAsync(CancellationToken.None);
        Assert.NotNull(await NotesAsync(database));

        await ExecuteAsync(owner, "UPDATE \"RetentionSettings\" SET \"BusinessEvidenceYears\" = 7");
        await Worker(data.Factory).RunBatchAsync(CancellationToken.None);
        Assert.Null(await NotesAsync(database));
    }

    private static async Task AgeCheckInAsync(NpgsqlConnection owner, string age)
    {
        await ExecuteAsync(owner, "ALTER TABLE \"AppointmentCheckIns\" DISABLE TRIGGER protect_checkin_evidence; " +
            $"UPDATE \"AppointmentCheckIns\" SET \"CheckedInAt\" = now() - interval '{age}'; " +
            "ALTER TABLE \"AppointmentCheckIns\" ENABLE TRIGGER protect_checkin_evidence;");
    }

    private static async Task<string?> NotesAsync(DealershipDbContext database)
    {
        return await database.Database.SqlQueryRaw<string?>("SELECT \"Notes\" AS \"Value\" FROM \"AppointmentCheckIns\"")
            .SingleAsync();
    }
}

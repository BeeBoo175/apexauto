using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Dealership.IntegrationTests;

public sealed class TestDriveMigrationTests(RegistrationDatabase fixture) : IClassFixture<RegistrationDatabase>
{
    [Fact]
    public async Task HandlingMigrationRestoresAssignmentGuards()
    {
        await using var maintenance = new DealershipDbContext(new DbContextOptionsBuilder<DealershipDbContext>()
            .UseNpgsql(fixture.ConnectionString).Options);
        var migrator = maintenance.GetService<IMigrator>();
        await migrator.MigrateAsync("20261103070027_TestDriveCustomerCancellations");
        await migrator.MigrateAsync("20261103095832_TestDriveAssignments");
        var original = await DefinitionsAsync();
        Assert.Equal(2, original.Length);
        await migrator.MigrateAsync("20261103160645_TestDriveHandling");
        var handling = await DefinitionsAsync();
        Assert.All(original.Zip(handling), pair => Assert.NotEqual(pair.First, pair.Second));
        Assert.Contains("Active drives require a reasoned manager handoff", handling[0]);
        Assert.Contains("\"TestDriveStarts\"", handling[0]);
        Assert.Contains("\"TestDriveOutcomes\"", handling[0]);
        Assert.Contains("\"TestDriveOccupations\"", handling[1]);
        Assert.Contains("\"TestDriveOutcomes\"", handling[1]);
        await migrator.MigrateAsync("20261103151704_TestDriveInspectionRepairCancellations");
        Assert.Equal(original, await DefinitionsAsync());
        await migrator.MigrateAsync("20261103160645_TestDriveHandling");
        Assert.Equal(handling, await DefinitionsAsync());
        await migrator.MigrateAsync();

        async Task<string[]> DefinitionsAsync()
        {
            var definitions = await maintenance.Database.SqlQueryRaw<string>("""
                SELECT pg_get_functiondef(oid) AS "Value" FROM pg_proc
                WHERE pronamespace = 'public'::regnamespace AND proname IN (
                    'guard_test_drive_assignment', 'staff_schedule_overlaps')
                ORDER BY proname
                """).ToArrayAsync();

            return [.. definitions.Select(definition => definition.ReplaceLineEndings("\n"))];
        }
    }

    [Fact]
    public async Task HandlingMigrationRestoresAutomaticCancellationSelectors()
    {
        await using var maintenance = new DealershipDbContext(new DbContextOptionsBuilder<DealershipDbContext>()
            .UseNpgsql(fixture.ConnectionString).Options);
        var migrator = maintenance.GetService<IMigrator>();
        await migrator.MigrateAsync("20261103102951_TestDriveStaffCancellations");
        await migrator.MigrateAsync("20261103151704_TestDriveInspectionRepairCancellations");
        var original = await DefinitionsAsync();
        Assert.Equal(4, original.Length);
        await migrator.MigrateAsync("20261103160645_TestDriveHandling");
        var handling = await DefinitionsAsync();
        Assert.All(original.Zip(handling), pair =>
        {
            Assert.NotEqual(pair.First, pair.Second);
            Assert.Contains("NOT EXISTS (SELECT 1 FROM \"TestDriveStarts\"", pair.Second);
            Assert.Contains("NOT EXISTS (SELECT 1 FROM \"TestDriveOutcomes\"", pair.Second);
        });
        await migrator.MigrateAsync("20261103151704_TestDriveInspectionRepairCancellations");
        Assert.Equal(original, await DefinitionsAsync());
        await migrator.MigrateAsync("20261103160645_TestDriveHandling");
        Assert.Equal(handling, await DefinitionsAsync());
        await migrator.MigrateAsync();

        async Task<string[]> DefinitionsAsync()
        {
            var definitions = await maintenance.Database.SqlQueryRaw<string>("""
                SELECT pg_get_functiondef(oid) AS "Value" FROM pg_proc
                WHERE pronamespace = 'public'::regnamespace AND proname IN (
                    'cancel_reserved_vehicle_test_drives', 'cancel_withdrawn_listing_test_drives',
                    'cancel_reopened_inspection_test_drives', 'cancel_inspection_repair_test_drives')
                ORDER BY proname
                """).ToArrayAsync();

            return [.. definitions.Select(definition => definition.ReplaceLineEndings("\n"))];
        }
    }

    [Fact]
    public async Task HandlingMigrationRestoresBookingAdmission()
    {
        await using var maintenance = new DealershipDbContext(new DbContextOptionsBuilder<DealershipDbContext>()
            .UseNpgsql(fixture.ConnectionString).Options);
        var migrator = maintenance.GetService<IMigrator>();
        await migrator.MigrateAsync("20261103064119_TestDriveBookings");
        await migrator.MigrateAsync("20261103070027_TestDriveCustomerCancellations");
        var original = await DefinitionAsync();
        await migrator.MigrateAsync("20261103160645_TestDriveHandling");
        var handling = await DefinitionAsync();
        Assert.NotEqual(original, handling);
        Assert.Contains("\"TestDriveOccupations\"", handling);
        Assert.Contains("\"TestDriveOutcomes\"", handling);
        await migrator.MigrateAsync("20261103151704_TestDriveInspectionRepairCancellations");
        Assert.Equal(original, await DefinitionAsync());
        await migrator.MigrateAsync("20261103160645_TestDriveHandling");
        Assert.Equal(handling, await DefinitionAsync());
        await migrator.MigrateAsync();

        async Task<string> DefinitionAsync()
        {
            var definition = await maintenance.Database.SqlQueryRaw<string>("""
                SELECT pg_get_functiondef('guard_test_drive_booking()'::regprocedure) AS "Value"
                """).SingleAsync();

            return definition.ReplaceLineEndings("\n");
        }
    }

    [Theory]
    [InlineData("20261103110638_TestDriveReservationCancellations",
        "20261103113429_TestDriveListingWithdrawalCancellations",
        "20261103122300_TestDriveInspectionReopeningCancellations",
        "System cancellation requires matching inspection reopening and current chronology")]
    [InlineData("20261103113429_TestDriveListingWithdrawalCancellations",
        "20261103122300_TestDriveInspectionReopeningCancellations",
        "20261103151704_TestDriveInspectionRepairCancellations",
        "System cancellation requires matching inspection repair request and current chronology")]
    public async Task InspectionCancellationMigrationRestoresPredecessorGuard(
        string beforePredecessor, string predecessor, string migration, string causeGuard)
    {
        await using var maintenance = new DealershipDbContext(new DbContextOptionsBuilder<DealershipDbContext>()
            .UseNpgsql(fixture.ConnectionString).Options);
        var migrator = maintenance.GetService<IMigrator>();
        await migrator.MigrateAsync(beforePredecessor);
        await migrator.MigrateAsync(predecessor);
        var original = await DefinitionAsync();
        await migrator.MigrateAsync(migration);
        var installed = await DefinitionAsync();
        Assert.NotEqual(original, installed);
        Assert.Contains(causeGuard, installed);
        await migrator.MigrateAsync(predecessor);
        Assert.Equal(original, await DefinitionAsync());
        await migrator.MigrateAsync(migration);
        Assert.Equal(installed, await DefinitionAsync());
        await migrator.MigrateAsync();

        async Task<string> DefinitionAsync()
        {
            var definition = await maintenance.Database.SqlQueryRaw<string>("""
                SELECT pg_get_functiondef('guard_test_drive_cancellation()'::regprocedure) AS "Value"
                """).SingleAsync();

            return definition.ReplaceLineEndings("\n");
        }
    }

    [Fact]
    public async Task ListingWithdrawalMigrationRestoresReservationGuard()
    {
        await using var maintenance = new DealershipDbContext(new DbContextOptionsBuilder<DealershipDbContext>()
            .UseNpgsql(fixture.ConnectionString).Options);
        var migrator = maintenance.GetService<IMigrator>();
        await migrator.MigrateAsync("20261103102951_TestDriveStaffCancellations");
        await migrator.MigrateAsync("20261103110638_TestDriveReservationCancellations");
        var reservation = await DefinitionAsync();
        await migrator.MigrateAsync("20261103113429_TestDriveListingWithdrawalCancellations");
        var withdrawal = await DefinitionAsync();
        Assert.NotEqual(reservation, withdrawal);
        Assert.Contains("System cancellation requires a matching listing withdrawal and current chronology", withdrawal);
        await migrator.MigrateAsync("20261103110638_TestDriveReservationCancellations");
        Assert.Equal(reservation, await DefinitionAsync());
        await migrator.MigrateAsync("20261103113429_TestDriveListingWithdrawalCancellations");
        Assert.Equal(withdrawal, await DefinitionAsync());
        await migrator.MigrateAsync();

        async Task<string> DefinitionAsync()
        {
            var definition = await maintenance.Database.SqlQueryRaw<string>("""
                SELECT pg_get_functiondef('guard_test_drive_cancellation()'::regprocedure) AS "Value"
                """).SingleAsync();

            return definition.ReplaceLineEndings("\n");
        }
    }

    [Fact]
    public async Task ReservationCancellationMigrationRestoresStaffGuard()
    {
        await using var maintenance = new DealershipDbContext(new DbContextOptionsBuilder<DealershipDbContext>()
            .UseNpgsql(fixture.ConnectionString).Options);
        var migrator = maintenance.GetService<IMigrator>();
        await migrator.MigrateAsync("20261103095832_TestDriveAssignments");
        await migrator.MigrateAsync("20261103102951_TestDriveStaffCancellations");
        var staff = await DefinitionAsync();
        await migrator.MigrateAsync("20261103110638_TestDriveReservationCancellations");
        var reservation = await DefinitionAsync();
        Assert.NotEqual(staff, reservation);
        Assert.Contains("System cancellation requires the vehicle reservation and current chronology", reservation);
        await migrator.MigrateAsync("20261103102951_TestDriveStaffCancellations");
        Assert.Equal(staff, await DefinitionAsync());
        await migrator.MigrateAsync("20261103110638_TestDriveReservationCancellations");
        Assert.Equal(reservation, await DefinitionAsync());
        await migrator.MigrateAsync();

        async Task<string> DefinitionAsync()
        {
            var definition = await maintenance.Database.SqlQueryRaw<string>("""
                SELECT pg_get_functiondef('guard_test_drive_cancellation()'::regprocedure) AS "Value"
                """).SingleAsync();

            return definition.ReplaceLineEndings("\n");
        }
    }

    [Fact]
    public async Task StaffCancellationMigrationRestoresCustomerGuard()
    {
        await using var maintenance = new DealershipDbContext(new DbContextOptionsBuilder<DealershipDbContext>()
            .UseNpgsql(fixture.ConnectionString).Options);
        var migrator = maintenance.GetService<IMigrator>();
        await migrator.MigrateAsync("20261103064119_TestDriveBookings");
        await migrator.MigrateAsync("20261103095832_TestDriveAssignments");
        var customer = await DefinitionAsync();
        await migrator.MigrateAsync("20261103102951_TestDriveStaffCancellations");
        var staff = await DefinitionAsync();
        Assert.NotEqual(customer, staff);
        Assert.Contains("Staff cancellation requires the current handler or branch manager", staff);
        await migrator.MigrateAsync("20261103095832_TestDriveAssignments");
        Assert.Equal(customer, await DefinitionAsync());
        await migrator.MigrateAsync("20261103102951_TestDriveStaffCancellations");
        Assert.Equal(staff, await DefinitionAsync());
        await migrator.MigrateAsync();

        async Task<string> DefinitionAsync()
        {
            var definition = await maintenance.Database.SqlQueryRaw<string>("""
                SELECT pg_get_functiondef('guard_test_drive_cancellation()'::regprocedure) AS "Value"
                """).SingleAsync();

            return definition.ReplaceLineEndings("\n");
        }
    }

    [Fact]
    public async Task AssignmentMigrationRestoresIntakeAdmissionAndPreservesCheckIn()
    {
        await using var maintenance = new DealershipDbContext(new DbContextOptionsBuilder<DealershipDbContext>()
            .UseNpgsql(fixture.ConnectionString).Options);
        var migrator = maintenance.GetService<IMigrator>();
        await migrator.MigrateAsync("20261101184925_SaleDocumentCorrections");
        await migrator.MigrateAsync("20261102193103_RequireCheckInHandler");
        var original = await DefinitionAsync("guard_appointment_assignment");
        var checkIn = await DefinitionAsync("validate_appointment_checkin");
        await migrator.MigrateAsync("20261103095832_TestDriveAssignments");
        var assignment = await DefinitionAsync("guard_appointment_assignment");
        Assert.NotEqual(original, assignment);
        Assert.Contains("public.staff_schedule_overlaps(NEW.\"HandlerId\", appointment.\"StartsAt\", appointment.\"EndsAt\", appointment.\"Id\", NULL)", assignment);
        Assert.Equal(checkIn, await DefinitionAsync("validate_appointment_checkin"));
        await migrator.MigrateAsync("20261103070027_TestDriveCustomerCancellations");
        Assert.Equal(original, await DefinitionAsync("guard_appointment_assignment"));
        Assert.Equal(checkIn, await DefinitionAsync("validate_appointment_checkin"));
        await migrator.MigrateAsync("20261103095832_TestDriveAssignments");
        Assert.Equal(assignment, await DefinitionAsync("guard_appointment_assignment"));
        Assert.Equal(checkIn, await DefinitionAsync("validate_appointment_checkin"));
        await migrator.MigrateAsync();

        async Task<string> DefinitionAsync(string name)
        {
            var definition = await maintenance.Database.SqlQuery<string>($"""
                SELECT pg_get_functiondef(oid) AS "Value" FROM pg_proc
                WHERE pronamespace = 'public'::regnamespace AND proname = {name}
                """).SingleAsync();

            return definition.ReplaceLineEndings("\n");
        }
    }

    [Fact]
    public async Task CustomerCancellationMigrationRestoresAdmissionDefinitions()
    {
        await using var maintenance = new DealershipDbContext(new DbContextOptionsBuilder<DealershipDbContext>()
            .UseNpgsql(fixture.ConnectionString).Options);
        var migrator = maintenance.GetService<IMigrator>();
        await migrator.MigrateAsync("20261102193103_RequireCheckInHandler");
        await migrator.MigrateAsync("20261103064119_TestDriveBookings");
        var original = await AdmissionDefinitionsAsync();
        Assert.Equal(3, original.Length);
        await migrator.MigrateAsync("20261103070027_TestDriveCustomerCancellations");
        var cancellation = await AdmissionDefinitionsAsync();
        Assert.All(original.Zip(cancellation), pair => Assert.NotEqual(pair.First, pair.Second));
        await migrator.MigrateAsync("20261103064119_TestDriveBookings");
        Assert.Equal(original, await AdmissionDefinitionsAsync());
        await migrator.MigrateAsync("20261103070027_TestDriveCustomerCancellations");
        Assert.Equal(cancellation, await AdmissionDefinitionsAsync());
        await migrator.MigrateAsync();

        async Task<string[]> AdmissionDefinitionsAsync()
        {
            var definitions = await maintenance.Database.SqlQueryRaw<string>("""
                SELECT pg_get_functiondef(oid) AS "Value" FROM pg_proc
                WHERE pronamespace = 'public'::regnamespace AND proname IN (
                    'guard_test_drive_booking', 'guard_test_drive_mail', 'require_test_drive_confirmation')
                ORDER BY proname
                """).ToArrayAsync();

            return [.. definitions.Select(definition => definition.ReplaceLineEndings("\n"))];
        }
    }
}

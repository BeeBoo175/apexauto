using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dealership.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TestDriveReservationCancellations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_TestDriveCancellations_Actor",
                table: "TestDriveCancellations");

            migrationBuilder.AddColumn<Guid>(
                name: "ReservationId",
                table: "TestDriveCancellations",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TestDriveCancellations_ReservationId",
                table: "TestDriveCancellations",
                column: "ReservationId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TestDriveCancellations_Actor",
                table: "TestDriveCancellations",
                sql: "(\"Kind\" = 'CustomerCancelled' AND \"StaffId\" IS NULL AND \"Reason\" IS NULL AND \"ReservationId\" IS NULL) OR (\"Kind\" = 'StaffCancelled' AND \"StaffId\" IS NOT NULL AND \"ReservationId\" IS NULL AND \"Reason\" IS NOT NULL AND length(\"Reason\") BETWEEN 1 AND 1000 AND \"Reason\" = btrim(\"Reason\") AND \"Reason\" ~ '[^[:space:]]' AND \"Reason\" !~ '[[:cntrl:]]') OR (\"Kind\" = 'VehicleReserved' AND \"StaffId\" IS NULL AND \"ReservationId\" IS NOT NULL AND \"Reason\" IS NOT NULL AND \"Reason\" = 'Vehicle reserved')");

            migrationBuilder.AddForeignKey(
                name: "FK_TestDriveCancellations_Reservations_ReservationId",
                table: "TestDriveCancellations",
                column: "ReservationId",
                principalTable: "Reservations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            UpdateCancellationGuard(migrationBuilder, restoring: false);
            migrationBuilder.Sql("""
                CREATE FUNCTION cancel_reserved_vehicle_test_drives() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE recorded_at timestamptz := clock_timestamp();
                BEGIN
                    -- Reservation admission already owns the vehicle; never upgrade a branch lock here.
                    WITH cancelled AS (
                        INSERT INTO "TestDriveCancellations"
                            ("Id", "BookingId", "CustomerId", "Kind", "ReservationId", "Reason", "RecordedAt")
                        SELECT gen_random_uuid(), b."Id", b."CustomerId", 'VehicleReserved', NEW."Id",
                            'Vehicle reserved', recorded_at
                        FROM "TestDriveBookings" b
                        WHERE b."VehicleId" = NEW."VehicleId" AND
                            recorded_at < ((b."StartsAt" AT TIME ZONE 'Asia/Kuala_Lumpur')::date + 1)::timestamp
                                AT TIME ZONE 'Asia/Kuala_Lumpur' AND
                            NOT EXISTS (SELECT 1 FROM "TestDriveCancellations" c WHERE c."BookingId" = b."Id")
                        ORDER BY b."Id"
                        RETURNING "BookingId")
                    INSERT INTO "TestDriveMailOutbox"
                        ("Id", "BookingId", "Kind", "RecipientEmail", "DeliveryAttempts", "NextDeliveryAt")
                    SELECT gen_random_uuid(), c."BookingId", 'Cancellation', m."RecipientEmail", 0, recorded_at
                    FROM cancelled c JOIN "TestDriveMailOutbox" m ON m."BookingId" = c."BookingId"
                        AND m."Kind" = 'Confirmation';
                    RETURN NULL;
                END $$;
                CREATE TRIGGER reservation_test_drive_cancellations AFTER INSERT ON "Reservations"
                    FOR EACH ROW EXECUTE FUNCTION cancel_reserved_vehicle_test_drives();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "TestDriveCancellations" WHERE "ReservationId" IS NOT NULL) THEN
                        RAISE EXCEPTION 'Retained reservation cancellations prevent rollback';
                    END IF;
                END $$;
                DROP TRIGGER reservation_test_drive_cancellations ON "Reservations";
                DROP FUNCTION cancel_reserved_vehicle_test_drives();
                """);
            UpdateCancellationGuard(migrationBuilder, restoring: true);

            migrationBuilder.DropForeignKey(
                name: "FK_TestDriveCancellations_Reservations_ReservationId",
                table: "TestDriveCancellations");

            migrationBuilder.DropIndex(
                name: "IX_TestDriveCancellations_ReservationId",
                table: "TestDriveCancellations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TestDriveCancellations_Actor",
                table: "TestDriveCancellations");

            migrationBuilder.DropColumn(
                name: "ReservationId",
                table: "TestDriveCancellations");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TestDriveCancellations_Actor",
                table: "TestDriveCancellations",
                sql: "(\"Kind\" = 'CustomerCancelled' AND \"StaffId\" IS NULL AND \"Reason\" IS NULL) OR (\"Kind\" = 'StaffCancelled' AND \"StaffId\" IS NOT NULL AND \"Reason\" IS NOT NULL AND length(\"Reason\") BETWEEN 1 AND 1000 AND \"Reason\" = btrim(\"Reason\") AND \"Reason\" ~ '[^[:space:]]' AND \"Reason\" !~ '[[:cntrl:]]')");
        }

        private static void UpdateCancellationGuard(MigrationBuilder migrationBuilder, bool restoring)
        {
            const string original = "PERFORM pg_advisory_xact_lock(hashtextextended('test-drive-cancellation:' || NEW.\"Id\"::text, 0));";
            const string systemGuard = """
                IF NEW."Kind" = 'VehicleReserved' THEN
                    SELECT * INTO booking FROM "TestDriveBookings" WHERE "Id" = NEW."BookingId";
                    PERFORM 1 FROM "Vehicles" WHERE "Id" = booking."VehicleId" FOR UPDATE;
                    IF booking."Id" IS NULL OR booking."CustomerId" <> NEW."CustomerId" OR
                       NEW."RecordedAt" < booking."CreatedAt" OR NEW."RecordedAt" > clock_timestamp() OR
                       clock_timestamp() >= ((booking."StartsAt" AT TIME ZONE 'Asia/Kuala_Lumpur')::date + 1)::timestamp
                           AT TIME ZONE 'Asia/Kuala_Lumpur' OR
                       EXISTS (SELECT 1 FROM "TestDriveAssignments" WHERE "BookingId" = booking."Id"
                           AND "RecordedAt" > NEW."RecordedAt") OR
                       NOT EXISTS (SELECT 1 FROM "Reservations" r WHERE r."Id" = NEW."ReservationId" AND
                           r."VehicleId" = booking."VehicleId" AND r."BranchId" = booking."BranchId" AND
                           r."IsActive" AND r."CreatedAt" <= NEW."RecordedAt") THEN
                        RAISE EXCEPTION 'System cancellation requires the vehicle reservation and current chronology'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_TestDriveCancellations_Reservation';
                    END IF;
                    RETURN NEW;
                END IF;
                """;
            var replacement = systemGuard + "\n" + original;
            var before = restoring ? replacement : original;
            var after = restoring ? original : replacement;
            migrationBuilder.Sql($"""
                DO $migration$
                DECLARE definition text := pg_get_functiondef('guard_test_drive_cancellation()'::regprocedure);
                BEGIN
                    IF (length(definition) - length(replace(definition, '{before.Replace("'", "''")}', ''))) /
                        length('{before.Replace("'", "''")}') <> 1 THEN
                        RAISE EXCEPTION 'Unexpected test-drive cancellation definition';
                    END IF;
                    EXECUTE replace(definition, '{before.Replace("'", "''")}', '{after.Replace("'", "''")}');
                END $migration$;
                """);
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dealership.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TestDriveInspectionReopeningCancellations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_TestDriveCancellations_Actor",
                table: "TestDriveCancellations");

            migrationBuilder.AddColumn<Guid>(
                name: "InspectionReopeningId",
                table: "TestDriveCancellations",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TestDriveCancellations_InspectionReopeningId",
                table: "TestDriveCancellations",
                column: "InspectionReopeningId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TestDriveCancellations_Actor",
                table: "TestDriveCancellations",
                sql: "(\"Kind\" = 'CustomerCancelled' AND \"StaffId\" IS NULL AND \"Reason\" IS NULL AND \"ReservationId\" IS NULL AND \"ListingWithdrawalId\" IS NULL AND \"InspectionReopeningId\" IS NULL) OR (\"Kind\" = 'StaffCancelled' AND \"StaffId\" IS NOT NULL AND \"ReservationId\" IS NULL AND \"ListingWithdrawalId\" IS NULL AND \"Reason\" IS NOT NULL AND \"InspectionReopeningId\" IS NULL AND length(\"Reason\") BETWEEN 1 AND 1000 AND \"Reason\" = btrim(\"Reason\") AND \"Reason\" ~ '[^[:space:]]' AND \"Reason\" !~ '[[:cntrl:]]') OR (\"Kind\" = 'VehicleReserved' AND \"StaffId\" IS NULL AND \"ReservationId\" IS NOT NULL AND \"ListingWithdrawalId\" IS NULL AND \"InspectionReopeningId\" IS NULL AND \"Reason\" IS NOT NULL AND \"Reason\" = 'Vehicle reserved') OR (\"Kind\" = 'ListingWithdrawn' AND \"StaffId\" IS NULL AND \"ReservationId\" IS NULL AND \"ListingWithdrawalId\" IS NOT NULL AND \"InspectionReopeningId\" IS NULL AND \"Reason\" IS NOT NULL AND \"Reason\" = 'Listing withdrawn') OR (\"Kind\" = 'InspectionReopened' AND \"StaffId\" IS NULL AND \"ReservationId\" IS NULL AND \"ListingWithdrawalId\" IS NULL AND \"InspectionReopeningId\" IS NOT NULL AND \"Reason\" IS NOT NULL AND \"Reason\" = 'Vehicle requires reinspection')");

            migrationBuilder.AddForeignKey(
                name: "FK_TestDriveCancellations_InspectionReopenings_InspectionReope~",
                table: "TestDriveCancellations",
                column: "InspectionReopeningId",
                principalTable: "InspectionReopenings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            UpdateCancellationGuard(migrationBuilder, restoring: false);
            migrationBuilder.Sql("""
                CREATE FUNCTION cancel_reopened_inspection_test_drives() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE
                    recorded_at timestamptz := clock_timestamp();
                    vehicle_id uuid;
                BEGIN
                    -- Reopening admission owns the vehicle; do not take a later branch or customer lock.
                    SELECT "VehicleId" INTO vehicle_id FROM "InspectionDrafts" WHERE "Id" = NEW."InspectionId";
                    WITH cancelled AS (
                        INSERT INTO "TestDriveCancellations"
                            ("Id", "BookingId", "CustomerId", "Kind", "InspectionReopeningId", "Reason", "RecordedAt")
                        SELECT gen_random_uuid(), b."Id", b."CustomerId", 'InspectionReopened', NEW."Id",
                            'Vehicle requires reinspection', recorded_at
                        FROM "TestDriveBookings" b
                        WHERE b."VehicleId" = vehicle_id AND
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
                CREATE TRIGGER inspection_test_drive_cancellations AFTER INSERT ON "InspectionReopenings"
                    FOR EACH ROW EXECUTE FUNCTION cancel_reopened_inspection_test_drives();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "TestDriveCancellations" WHERE "InspectionReopeningId" IS NOT NULL) THEN
                        RAISE EXCEPTION 'Retained inspection reopening cancellations prevent rollback';
                    END IF;
                END $$;
                DROP TRIGGER inspection_test_drive_cancellations ON "InspectionReopenings";
                DROP FUNCTION cancel_reopened_inspection_test_drives();
                """);
            UpdateCancellationGuard(migrationBuilder, restoring: true);

            migrationBuilder.DropForeignKey(
                name: "FK_TestDriveCancellations_InspectionReopenings_InspectionReope~",
                table: "TestDriveCancellations");

            migrationBuilder.DropIndex(
                name: "IX_TestDriveCancellations_InspectionReopeningId",
                table: "TestDriveCancellations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TestDriveCancellations_Actor",
                table: "TestDriveCancellations");

            migrationBuilder.DropColumn(
                name: "InspectionReopeningId",
                table: "TestDriveCancellations");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TestDriveCancellations_Actor",
                table: "TestDriveCancellations",
                sql: "(\"Kind\" = 'CustomerCancelled' AND \"StaffId\" IS NULL AND \"Reason\" IS NULL AND \"ReservationId\" IS NULL AND \"ListingWithdrawalId\" IS NULL) OR (\"Kind\" = 'StaffCancelled' AND \"StaffId\" IS NOT NULL AND \"ReservationId\" IS NULL AND \"ListingWithdrawalId\" IS NULL AND \"Reason\" IS NOT NULL AND length(\"Reason\") BETWEEN 1 AND 1000 AND \"Reason\" = btrim(\"Reason\") AND \"Reason\" ~ '[^[:space:]]' AND \"Reason\" !~ '[[:cntrl:]]') OR (\"Kind\" = 'VehicleReserved' AND \"StaffId\" IS NULL AND \"ReservationId\" IS NOT NULL AND \"ListingWithdrawalId\" IS NULL AND \"Reason\" IS NOT NULL AND \"Reason\" = 'Vehicle reserved') OR (\"Kind\" = 'ListingWithdrawn' AND \"StaffId\" IS NULL AND \"ReservationId\" IS NULL AND \"ListingWithdrawalId\" IS NOT NULL AND \"Reason\" IS NOT NULL AND \"Reason\" = 'Listing withdrawn')");
        }

        private static void UpdateCancellationGuard(MigrationBuilder migrationBuilder, bool restoring)
        {
            const string original = "IF NEW.\"Kind\" = 'ListingWithdrawn' THEN";
            const string reopeningGuard = """
                IF NEW."Kind" = 'InspectionReopened' THEN
                    SELECT * INTO booking FROM "TestDriveBookings" WHERE "Id" = NEW."BookingId";
                    PERFORM 1 FROM "Vehicles" WHERE "Id" = booking."VehicleId" FOR UPDATE;
                    IF booking."Id" IS NULL OR booking."CustomerId" <> NEW."CustomerId" OR
                       NEW."RecordedAt" < booking."CreatedAt" OR NEW."RecordedAt" > clock_timestamp() OR
                       clock_timestamp() >= ((booking."StartsAt" AT TIME ZONE 'Asia/Kuala_Lumpur')::date + 1)::timestamp
                           AT TIME ZONE 'Asia/Kuala_Lumpur' OR
                       EXISTS (SELECT 1 FROM "TestDriveAssignments" WHERE "BookingId" = booking."Id"
                           AND "RecordedAt" > NEW."RecordedAt") OR
                       NOT EXISTS (SELECT 1 FROM "InspectionReopenings" r
                           JOIN "InspectionDrafts" d ON d."Id" = r."InspectionId" AND d."BranchId" = r."BranchId"
                           WHERE r."Id" = NEW."InspectionReopeningId" AND d."VehicleId" = booking."VehicleId" AND
                               r."BranchId" = booking."BranchId" AND r."RecordedAt" >= booking."CreatedAt" AND
                               r."RecordedAt" <= NEW."RecordedAt") THEN
                        RAISE EXCEPTION 'System cancellation requires matching inspection reopening and current chronology'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_TestDriveCancellations_Reopening';
                    END IF;
                    RETURN NEW;
                END IF;
                """;
            var replacement = reopeningGuard + "\n" + original;
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

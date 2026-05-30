using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dealership.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TestDriveListingWithdrawalCancellations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_TestDriveCancellations_Actor",
                table: "TestDriveCancellations");

            migrationBuilder.AddColumn<Guid>(
                name: "ListingWithdrawalId",
                table: "TestDriveCancellations",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TestDriveCancellations_ListingWithdrawalId",
                table: "TestDriveCancellations",
                column: "ListingWithdrawalId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TestDriveCancellations_Actor",
                table: "TestDriveCancellations",
                sql: "(\"Kind\" = 'CustomerCancelled' AND \"StaffId\" IS NULL AND \"Reason\" IS NULL AND \"ReservationId\" IS NULL AND \"ListingWithdrawalId\" IS NULL) OR (\"Kind\" = 'StaffCancelled' AND \"StaffId\" IS NOT NULL AND \"ReservationId\" IS NULL AND \"ListingWithdrawalId\" IS NULL AND \"Reason\" IS NOT NULL AND length(\"Reason\") BETWEEN 1 AND 1000 AND \"Reason\" = btrim(\"Reason\") AND \"Reason\" ~ '[^[:space:]]' AND \"Reason\" !~ '[[:cntrl:]]') OR (\"Kind\" = 'VehicleReserved' AND \"StaffId\" IS NULL AND \"ReservationId\" IS NOT NULL AND \"ListingWithdrawalId\" IS NULL AND \"Reason\" IS NOT NULL AND \"Reason\" = 'Vehicle reserved') OR (\"Kind\" = 'ListingWithdrawn' AND \"StaffId\" IS NULL AND \"ReservationId\" IS NULL AND \"ListingWithdrawalId\" IS NOT NULL AND \"Reason\" IS NOT NULL AND \"Reason\" = 'Listing withdrawn')");

            migrationBuilder.AddForeignKey(
                name: "FK_TestDriveCancellations_ListingWithdrawals_ListingWithdrawal~",
                table: "TestDriveCancellations",
                column: "ListingWithdrawalId",
                principalTable: "ListingWithdrawals",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            UpdateCancellationGuard(migrationBuilder, restoring: false);
            migrationBuilder.Sql("""
                CREATE FUNCTION cancel_withdrawn_listing_test_drives() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE recorded_at timestamptz := clock_timestamp();
                BEGIN
                    -- Withdrawal admission owns the vehicle; cancellation must not acquire a later branch lock.
                    WITH cancelled AS (
                        INSERT INTO "TestDriveCancellations"
                            ("Id", "BookingId", "CustomerId", "Kind", "ListingWithdrawalId", "Reason", "RecordedAt")
                        SELECT gen_random_uuid(), b."Id", b."CustomerId", 'ListingWithdrawn', NEW."Id",
                            'Listing withdrawn', recorded_at
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
                CREATE TRIGGER listing_test_drive_cancellations AFTER INSERT ON "ListingWithdrawals"
                    FOR EACH ROW EXECUTE FUNCTION cancel_withdrawn_listing_test_drives();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "TestDriveCancellations" WHERE "ListingWithdrawalId" IS NOT NULL) THEN
                        RAISE EXCEPTION 'Retained listing withdrawal cancellations prevent rollback';
                    END IF;
                END $$;
                DROP TRIGGER listing_test_drive_cancellations ON "ListingWithdrawals";
                DROP FUNCTION cancel_withdrawn_listing_test_drives();
                """);
            UpdateCancellationGuard(migrationBuilder, restoring: true);

            migrationBuilder.DropForeignKey(
                name: "FK_TestDriveCancellations_ListingWithdrawals_ListingWithdrawal~",
                table: "TestDriveCancellations");

            migrationBuilder.DropIndex(
                name: "IX_TestDriveCancellations_ListingWithdrawalId",
                table: "TestDriveCancellations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TestDriveCancellations_Actor",
                table: "TestDriveCancellations");

            migrationBuilder.DropColumn(
                name: "ListingWithdrawalId",
                table: "TestDriveCancellations");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TestDriveCancellations_Actor",
                table: "TestDriveCancellations",
                sql: "(\"Kind\" = 'CustomerCancelled' AND \"StaffId\" IS NULL AND \"Reason\" IS NULL AND \"ReservationId\" IS NULL) OR (\"Kind\" = 'StaffCancelled' AND \"StaffId\" IS NOT NULL AND \"ReservationId\" IS NULL AND \"Reason\" IS NOT NULL AND length(\"Reason\") BETWEEN 1 AND 1000 AND \"Reason\" = btrim(\"Reason\") AND \"Reason\" ~ '[^[:space:]]' AND \"Reason\" !~ '[[:cntrl:]]') OR (\"Kind\" = 'VehicleReserved' AND \"StaffId\" IS NULL AND \"ReservationId\" IS NOT NULL AND \"Reason\" IS NOT NULL AND \"Reason\" = 'Vehicle reserved')");
        }

        private static void UpdateCancellationGuard(MigrationBuilder migrationBuilder, bool restoring)
        {
            const string original = "IF NEW.\"Kind\" = 'VehicleReserved' THEN";
            const string withdrawalGuard = """
                IF NEW."Kind" = 'ListingWithdrawn' THEN
                    SELECT * INTO booking FROM "TestDriveBookings" WHERE "Id" = NEW."BookingId";
                    PERFORM 1 FROM "Vehicles" WHERE "Id" = booking."VehicleId" FOR UPDATE;
                    IF booking."Id" IS NULL OR booking."CustomerId" <> NEW."CustomerId" OR
                       NEW."RecordedAt" < booking."CreatedAt" OR NEW."RecordedAt" > clock_timestamp() OR
                       clock_timestamp() >= ((booking."StartsAt" AT TIME ZONE 'Asia/Kuala_Lumpur')::date + 1)::timestamp
                           AT TIME ZONE 'Asia/Kuala_Lumpur' OR
                       EXISTS (SELECT 1 FROM "TestDriveAssignments" WHERE "BookingId" = booking."Id"
                           AND "RecordedAt" > NEW."RecordedAt") OR
                       NOT EXISTS (SELECT 1 FROM "ListingWithdrawals" w WHERE w."Id" = NEW."ListingWithdrawalId" AND
                           w."VehicleId" = booking."VehicleId" AND w."BranchId" = booking."BranchId" AND
                           w."RecordedAt" >= booking."CreatedAt" AND w."RecordedAt" <= NEW."RecordedAt") THEN
                        RAISE EXCEPTION 'System cancellation requires a matching listing withdrawal and current chronology'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_TestDriveCancellations_Withdrawal';
                    END IF;
                    RETURN NEW;
                END IF;
                """;
            var replacement = withdrawalGuard + "\n" + original;
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

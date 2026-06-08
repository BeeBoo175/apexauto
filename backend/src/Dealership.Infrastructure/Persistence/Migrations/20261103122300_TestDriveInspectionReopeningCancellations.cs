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

            InstallCancellationGuard(migrationBuilder);
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
            RestoreCancellationGuard(migrationBuilder);

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

        private static void InstallCancellationGuard(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION guard_test_drive_cancellation() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE
                    booking "TestDriveBookings"%ROWTYPE;
                    customer "AspNetUsers"%ROWTYPE;
                BEGIN
                    IF TG_OP <> 'INSERT' THEN
                        RAISE EXCEPTION 'Test-drive cancellation receipts are retained and immutable'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_TestDriveCancellations_Immutable';
                    END IF;
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
                    PERFORM pg_advisory_xact_lock(hashtextextended('test-drive-cancellation:' || NEW."Id"::text, 0));
                    SELECT * INTO customer FROM "AspNetUsers" WHERE "Id" = CASE WHEN NEW."Kind" = 'StaffCancelled' THEN NEW."StaffId" ELSE NEW."CustomerId" END FOR UPDATE;
                    IF NEW."Kind" = 'CustomerCancelled' THEN
                        PERFORM 1 FROM "CustomerProfiles" WHERE "UserId" = NEW."CustomerId" FOR SHARE;
                    ELSE
                        PERFORM 1 WHERE NEW."Kind" = 'StaffCancelled';
                    END IF;
                    IF NOT FOUND OR customer."Id" IS NULL OR NOT customer."IsActive" OR NOT ((NEW."Kind" = 'CustomerCancelled' AND customer."Role" = 'Customer') OR (NEW."Kind" = 'StaffCancelled' AND customer."Role" IN ('SalesAgent', 'BranchManager'))) THEN
                        RAISE EXCEPTION 'Cancellation requires a current customer'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_TestDriveCancellations_Customer';
                    END IF;
                    SELECT * INTO booking FROM "TestDriveBookings" WHERE "Id" = NEW."BookingId";
                    IF booking."Id" IS NULL OR booking."CustomerId" <> NEW."CustomerId" THEN
                        RAISE EXCEPTION 'Cancellation belongs to the booking customer'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_TestDriveCancellations_Owner';
                    END IF;
                    -- Availability writers acquire the vehicle before any dependent drive evidence.
                    UPDATE "Branches" SET "Name" = "Name" WHERE "Id" = booking."BranchId";
                    UPDATE "Vehicles" SET "Status" = "Status" WHERE "Id" = booking."VehicleId";
                    IF NEW."Kind" = 'StaffCancelled' AND (
                        customer."BranchId" IS DISTINCT FROM booking."BranchId" OR
                        NOT EXISTS (SELECT 1 FROM "Branches" WHERE "Id" = booking."BranchId" AND "IsActive") OR
                        (customer."Role" <> 'BranchManager' AND customer."Id" IS DISTINCT FROM (
                            SELECT "HandlerId" FROM "TestDriveAssignments" WHERE "BookingId" = booking."Id"
                            ORDER BY "Version" DESC LIMIT 1))) THEN
                        RAISE EXCEPTION 'Staff cancellation requires the current handler or branch manager'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_TestDriveCancellations_Staff';
                    END IF;
                    IF NEW."RecordedAt" < booking."CreatedAt" OR NEW."RecordedAt" > clock_timestamp() OR
                       (NEW."Kind" = 'CustomerCancelled' AND (NEW."RecordedAt" >= booking."StartsAt" OR clock_timestamp() >= booking."StartsAt")) OR
                       (NEW."Kind" = 'StaffCancelled' AND (
                           NEW."RecordedAt" >= ((booking."StartsAt" AT TIME ZONE 'Asia/Kuala_Lumpur')::date + 1)::timestamp AT TIME ZONE 'Asia/Kuala_Lumpur' OR
                           clock_timestamp() >= ((booking."StartsAt" AT TIME ZONE 'Asia/Kuala_Lumpur')::date + 1)::timestamp AT TIME ZONE 'Asia/Kuala_Lumpur')) OR
                       EXISTS (SELECT 1 FROM "TestDriveAssignments" WHERE "BookingId" = booking."Id" AND "RecordedAt" > NEW."RecordedAt") THEN
                        RAISE EXCEPTION 'Customer cancellation must precede booked start'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_TestDriveCancellations_Time';
                    END IF;
                    UPDATE "AspNetUsers" SET "ConcurrencyStamp" = "ConcurrencyStamp" WHERE "Id" = customer."Id";
                    RETURN NEW;
                END $$;
                """);
        }

        private static void RestoreCancellationGuard(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION guard_test_drive_cancellation() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE
                    booking "TestDriveBookings"%ROWTYPE;
                    customer "AspNetUsers"%ROWTYPE;
                BEGIN
                    IF TG_OP <> 'INSERT' THEN
                        RAISE EXCEPTION 'Test-drive cancellation receipts are retained and immutable'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_TestDriveCancellations_Immutable';
                    END IF;
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
                    PERFORM pg_advisory_xact_lock(hashtextextended('test-drive-cancellation:' || NEW."Id"::text, 0));
                    SELECT * INTO customer FROM "AspNetUsers" WHERE "Id" = CASE WHEN NEW."Kind" = 'StaffCancelled' THEN NEW."StaffId" ELSE NEW."CustomerId" END FOR UPDATE;
                    IF NEW."Kind" = 'CustomerCancelled' THEN
                        PERFORM 1 FROM "CustomerProfiles" WHERE "UserId" = NEW."CustomerId" FOR SHARE;
                    ELSE
                        PERFORM 1 WHERE NEW."Kind" = 'StaffCancelled';
                    END IF;
                    IF NOT FOUND OR customer."Id" IS NULL OR NOT customer."IsActive" OR NOT ((NEW."Kind" = 'CustomerCancelled' AND customer."Role" = 'Customer') OR (NEW."Kind" = 'StaffCancelled' AND customer."Role" IN ('SalesAgent', 'BranchManager'))) THEN
                        RAISE EXCEPTION 'Cancellation requires a current customer'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_TestDriveCancellations_Customer';
                    END IF;
                    SELECT * INTO booking FROM "TestDriveBookings" WHERE "Id" = NEW."BookingId";
                    IF booking."Id" IS NULL OR booking."CustomerId" <> NEW."CustomerId" THEN
                        RAISE EXCEPTION 'Cancellation belongs to the booking customer'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_TestDriveCancellations_Owner';
                    END IF;
                    -- Availability writers acquire the vehicle before any dependent drive evidence.
                    UPDATE "Branches" SET "Name" = "Name" WHERE "Id" = booking."BranchId";
                    UPDATE "Vehicles" SET "Status" = "Status" WHERE "Id" = booking."VehicleId";
                    IF NEW."Kind" = 'StaffCancelled' AND (
                        customer."BranchId" IS DISTINCT FROM booking."BranchId" OR
                        NOT EXISTS (SELECT 1 FROM "Branches" WHERE "Id" = booking."BranchId" AND "IsActive") OR
                        (customer."Role" <> 'BranchManager' AND customer."Id" IS DISTINCT FROM (
                            SELECT "HandlerId" FROM "TestDriveAssignments" WHERE "BookingId" = booking."Id"
                            ORDER BY "Version" DESC LIMIT 1))) THEN
                        RAISE EXCEPTION 'Staff cancellation requires the current handler or branch manager'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_TestDriveCancellations_Staff';
                    END IF;
                    IF NEW."RecordedAt" < booking."CreatedAt" OR NEW."RecordedAt" > clock_timestamp() OR
                       (NEW."Kind" = 'CustomerCancelled' AND (NEW."RecordedAt" >= booking."StartsAt" OR clock_timestamp() >= booking."StartsAt")) OR
                       (NEW."Kind" = 'StaffCancelled' AND (
                           NEW."RecordedAt" >= ((booking."StartsAt" AT TIME ZONE 'Asia/Kuala_Lumpur')::date + 1)::timestamp AT TIME ZONE 'Asia/Kuala_Lumpur' OR
                           clock_timestamp() >= ((booking."StartsAt" AT TIME ZONE 'Asia/Kuala_Lumpur')::date + 1)::timestamp AT TIME ZONE 'Asia/Kuala_Lumpur')) OR
                       EXISTS (SELECT 1 FROM "TestDriveAssignments" WHERE "BookingId" = booking."Id" AND "RecordedAt" > NEW."RecordedAt") THEN
                        RAISE EXCEPTION 'Customer cancellation must precede booked start'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_TestDriveCancellations_Time';
                    END IF;
                    UPDATE "AspNetUsers" SET "ConcurrencyStamp" = "ConcurrencyStamp" WHERE "Id" = customer."Id";
                    RETURN NEW;
                END $$;
                """);
        }
    }
}

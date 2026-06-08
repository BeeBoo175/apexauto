using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dealership.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TestDriveCustomerCancellations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TestDriveMailOutbox_BookingId",
                table: "TestDriveMailOutbox");

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "TestDriveMailOutbox",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Confirmation");

            migrationBuilder.AddColumn<Guid>(
                name: "CancellationBookingId",
                table: "TestDriveMailOutbox",
                type: "uuid",
                nullable: true,
                computedColumnSql: "CASE WHEN \"Kind\" = 'Cancellation' THEN \"BookingId\" END",
                stored: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_TestDriveBookings_Id_CustomerId",
                table: "TestDriveBookings",
                columns: ["Id", "CustomerId"]);

            migrationBuilder.CreateTable(
                name: "TestDriveCancellations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BookingId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestDriveCancellations", x => x.Id);
                    table.UniqueConstraint("AK_TestDriveCancellations_BookingId", x => x.BookingId);
                    table.CheckConstraint("CK_TestDriveCancellations_Shape", "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND isfinite(\"RecordedAt\")");
                    table.ForeignKey(
                        name: "FK_TestDriveCancellations_CustomerProfiles_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "CustomerProfiles",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TestDriveCancellations_TestDriveBookings_BookingId_Customer~",
                        columns: x => new { x.BookingId, x.CustomerId },
                        principalTable: "TestDriveBookings",
                        principalColumns: ["Id", "CustomerId"],
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TestDriveMailOutbox_BookingId_Kind",
                table: "TestDriveMailOutbox",
                columns: ["BookingId", "Kind"],
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TestDriveMailOutbox_CancellationBookingId",
                table: "TestDriveMailOutbox",
                column: "CancellationBookingId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TestDriveMailOutbox_Kind",
                table: "TestDriveMailOutbox",
                sql: "\"Kind\" IN ('Confirmation', 'Cancellation')");

            migrationBuilder.CreateIndex(
                name: "IX_TestDriveCancellations_BookingId_CustomerId",
                table: "TestDriveCancellations",
                columns: ["BookingId", "CustomerId"],
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TestDriveCancellations_CustomerId",
                table: "TestDriveCancellations",
                column: "CustomerId");

            migrationBuilder.AddForeignKey(
                name: "FK_TestDriveMailOutbox_TestDriveCancellations_CancellationBook~",
                table: "TestDriveMailOutbox",
                column: "CancellationBookingId",
                principalTable: "TestDriveCancellations",
                principalColumn: "BookingId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql("""
                ALTER TABLE "TestDriveMailOutbox" ALTER COLUMN "Kind" DROP DEFAULT;
                CREATE FUNCTION guard_test_drive_cancellation() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE
                    booking "TestDriveBookings"%ROWTYPE;
                    customer "AspNetUsers"%ROWTYPE;
                BEGIN
                    IF TG_OP <> 'INSERT' THEN
                        RAISE EXCEPTION 'Test-drive cancellation receipts are retained and immutable'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_TestDriveCancellations_Immutable';
                    END IF;
                    PERFORM pg_advisory_xact_lock(hashtextextended('test-drive-cancellation:' || NEW."Id"::text, 0));
                    SELECT * INTO customer FROM "AspNetUsers" WHERE "Id" = NEW."CustomerId" FOR UPDATE;
                    PERFORM 1 FROM "CustomerProfiles" WHERE "UserId" = NEW."CustomerId" FOR SHARE;
                    IF NOT FOUND OR customer."Id" IS NULL OR NOT customer."IsActive" OR customer."Role" <> 'Customer' THEN
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
                    IF NEW."RecordedAt" < booking."CreatedAt" OR NEW."RecordedAt" > clock_timestamp() OR
                       NEW."RecordedAt" >= booking."StartsAt" OR clock_timestamp() >= booking."StartsAt" THEN
                        RAISE EXCEPTION 'Customer cancellation must precede booked start'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_TestDriveCancellations_Time';
                    END IF;
                    UPDATE "AspNetUsers" SET "ConcurrencyStamp" = "ConcurrencyStamp" WHERE "Id" = customer."Id";
                    RETURN NEW;
                END $$;
                CREATE TRIGGER test_drive_cancellation_guard BEFORE INSERT OR UPDATE OR DELETE ON "TestDriveCancellations"
                    FOR EACH ROW EXECUTE FUNCTION guard_test_drive_cancellation();

                CREATE FUNCTION require_test_drive_cancellation_mail() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM "TestDriveMailOutbox" WHERE "BookingId" = NEW."BookingId" AND "Kind" = 'Cancellation') THEN
                        RAISE EXCEPTION 'Cancellation requires its durable customer notification'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_TestDriveCancellations_Mail';
                    END IF;
                    RETURN NULL;
                END $$;
                CREATE CONSTRAINT TRIGGER test_drive_cancellation_bundle AFTER INSERT ON "TestDriveCancellations"
                    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION require_test_drive_cancellation_mail();
                """);
            InstallAdmission(migrationBuilder);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "TestDriveCancellations") THEN
                        RAISE EXCEPTION 'Retained test-drive cancellations prevent rollback';
                    END IF;
                END $$;
                DROP TRIGGER test_drive_cancellation_bundle ON "TestDriveCancellations";
                DROP FUNCTION require_test_drive_cancellation_mail();
                DROP TRIGGER test_drive_cancellation_guard ON "TestDriveCancellations";
                DROP FUNCTION guard_test_drive_cancellation();
                """);
            RestoreAdmission(migrationBuilder);

            migrationBuilder.DropForeignKey(
                name: "FK_TestDriveMailOutbox_TestDriveCancellations_CancellationBook~",
                table: "TestDriveMailOutbox");

            migrationBuilder.DropTable(
                name: "TestDriveCancellations");

            migrationBuilder.DropIndex(
                name: "IX_TestDriveMailOutbox_BookingId_Kind",
                table: "TestDriveMailOutbox");

            migrationBuilder.DropIndex(
                name: "IX_TestDriveMailOutbox_CancellationBookingId",
                table: "TestDriveMailOutbox");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TestDriveMailOutbox_Kind",
                table: "TestDriveMailOutbox");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_TestDriveBookings_Id_CustomerId",
                table: "TestDriveBookings");

            migrationBuilder.DropColumn(
                name: "CancellationBookingId",
                table: "TestDriveMailOutbox");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "TestDriveMailOutbox");

            migrationBuilder.CreateIndex(
                name: "IX_TestDriveMailOutbox_BookingId",
                table: "TestDriveMailOutbox",
                column: "BookingId",
                unique: true);
        }

        private static void InstallAdmission(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION guard_test_drive_booking() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE
                    customer "AspNetUsers"%ROWTYPE;
                    vehicle "Vehicles"%ROWTYPE;
                    publication "ListingPublications"%ROWTYPE;
                    revision "ListingRevisions"%ROWTYPE;
                    today timestamptz;
                BEGIN
                    IF TG_OP <> 'INSERT' THEN
                        RAISE EXCEPTION 'Test-drive booking receipts are retained and immutable'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_TestDriveBookings_Immutable';
                    END IF;
                    PERFORM pg_advisory_xact_lock(hashtextextended('test-drive-booking:' || NEW."Id"::text, 0));
                    SELECT * INTO customer FROM "AspNetUsers" WHERE "Id" = NEW."CustomerId" FOR UPDATE;
                    PERFORM 1 FROM "CustomerProfiles" WHERE "UserId" = NEW."CustomerId" FOR SHARE;
                    IF NOT FOUND OR customer."Id" IS NULL OR NOT customer."IsActive" OR
                       NOT customer."EmailConfirmed" OR customer."Role" <> 'Customer' THEN
                        RAISE EXCEPTION 'A test drive requires a current verified customer'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_TestDriveBookings_Customer';
                    END IF;
                    UPDATE "Branches" SET "Name" = "Name" WHERE "Id" = NEW."BranchId" AND "IsActive";
                    IF NOT FOUND THEN
                        RAISE EXCEPTION 'Test-drive branch is unavailable'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_TestDriveBookings_Eligibility';
                    END IF;
                    SELECT * INTO vehicle FROM "Vehicles" WHERE "Id" = NEW."VehicleId" AND "BranchId" = NEW."BranchId" FOR UPDATE;
                    SELECT * INTO publication FROM "ListingPublications" WHERE "Id" = NEW."PublicationId";
                    SELECT * INTO revision FROM "ListingRevisions" WHERE "Id" = publication."RevisionId";
                    IF vehicle."Id" IS NULL OR vehicle."Status" <> 'Available' OR publication."Id" IS NULL OR
                       publication."VehicleId" <> NEW."VehicleId" OR publication."BranchId" <> NEW."BranchId" OR
                       NEW."CreatedAt" < publication."RecordedAt" OR NEW."CreatedAt" > clock_timestamp() OR
                       NEW."StartsAt" <= clock_timestamp() OR
                       EXISTS (SELECT 1 FROM "Reservations" WHERE "VehicleId" = vehicle."Id" AND "IsActive") OR
                       EXISTS (SELECT 1 FROM "ListingWithdrawals" WHERE "PublicationId" = publication."Id") OR
                       EXISTS (SELECT 1 FROM "ListingPublications" p JOIN "ListingRevisions" r ON r."Id" = p."RevisionId"
                           WHERE p."VehicleId" = vehicle."Id" AND r."Version" > revision."Version") OR
                       NOT EXISTS (SELECT 1 FROM "InspectionSignedRevisions" s JOIN "InspectionDrafts" d ON d."Id" = s."InspectionId"
                           WHERE s."Id" = publication."SignedRevisionId" AND d."VehicleId" = vehicle."Id" AND
                               d."IsSigned" AND d."Revision" = s."Revision") THEN
                        RAISE EXCEPTION 'Test drive requires the current available publication'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_TestDriveBookings_Eligibility';
                    END IF;
                    today := date_trunc('day', clock_timestamp() AT TIME ZONE 'Asia/Kuala_Lumpur') AT TIME ZONE 'Asia/Kuala_Lumpur';
                    IF EXISTS (SELECT 1 FROM "TestDriveBookings" WHERE "CustomerId" = NEW."CustomerId" AND "StartsAt" >= today AND NOT EXISTS (SELECT 1 FROM "TestDriveCancellations" c WHERE c."BookingId" = "TestDriveBookings"."Id")) THEN
                        RAISE EXCEPTION 'Customer already has an outstanding test drive'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_TestDriveBookings_CustomerLimit';
                    END IF;
                    IF EXISTS (SELECT 1 FROM "TestDriveBookings" WHERE "BranchId" = NEW."BranchId" AND
                        "StartsAt" < NEW."EndsAt" AND "EndsAt" > NEW."StartsAt" AND NOT EXISTS (SELECT 1 FROM "TestDriveCancellations" c WHERE c."BookingId" = "TestDriveBookings"."Id")) THEN
                        RAISE EXCEPTION 'Test-drive branch interval is occupied'
                            USING ERRCODE = '23P01', CONSTRAINT = 'EX_TestDriveBookings_Slot';
                    END IF;
                    -- Fence stale snapshots for both customer limits and competing vehicle availability writers.
                    UPDATE "AspNetUsers" SET "ConcurrencyStamp" = "ConcurrencyStamp" WHERE "Id" = customer."Id";
                    UPDATE "Vehicles" SET "Status" = "Status" WHERE "Id" = vehicle."Id";
                    RETURN NEW;
                END $$;

                CREATE OR REPLACE FUNCTION guard_test_drive_mail() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP = 'DELETE' OR (TG_OP = 'UPDATE' AND (
                        ROW(NEW."Id", NEW."BookingId", NEW."RecipientEmail", NEW."Kind") IS DISTINCT FROM
                        ROW(OLD."Id", OLD."BookingId", OLD."RecipientEmail", OLD."Kind") OR
                        (OLD."DeliveredAt" IS NOT NULL AND NEW."DeliveredAt" IS DISTINCT FROM OLD."DeliveredAt") OR
                        NEW."DeliveryAttempts" < OLD."DeliveryAttempts")) THEN
                        RAISE EXCEPTION 'Test-drive mail identity and first delivery receipt are immutable'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_TestDriveMailOutbox_Immutable';
                    END IF;
                    IF TG_OP = 'INSERT' AND NOT EXISTS (
                        SELECT 1 FROM "TestDriveBookings" b JOIN "AspNetUsers" u ON u."Id" = b."CustomerId"
                        WHERE b."Id" = NEW."BookingId" AND u."Email" = NEW."RecipientEmail") THEN
                        RAISE EXCEPTION 'Test-drive mail must retain the booking customer recipient'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_TestDriveMailOutbox_Recipient';
                    END IF;
                    RETURN NEW;
                END $$;

                CREATE OR REPLACE FUNCTION require_test_drive_confirmation() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM "TestDriveMailOutbox" WHERE "BookingId" = NEW."Id" AND "Kind" = 'Confirmation') THEN
                        RAISE EXCEPTION 'Test-drive booking requires its durable confirmation'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_TestDriveBookings_Confirmation';
                    END IF;
                    RETURN NULL;
                END $$;
                """);
        }

        private static void RestoreAdmission(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION guard_test_drive_booking() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE
                    customer "AspNetUsers"%ROWTYPE;
                    vehicle "Vehicles"%ROWTYPE;
                    publication "ListingPublications"%ROWTYPE;
                    revision "ListingRevisions"%ROWTYPE;
                    today timestamptz;
                BEGIN
                    IF TG_OP <> 'INSERT' THEN
                        RAISE EXCEPTION 'Test-drive booking receipts are retained and immutable'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_TestDriveBookings_Immutable';
                    END IF;
                    PERFORM pg_advisory_xact_lock(hashtextextended('test-drive-booking:' || NEW."Id"::text, 0));
                    SELECT * INTO customer FROM "AspNetUsers" WHERE "Id" = NEW."CustomerId" FOR UPDATE;
                    PERFORM 1 FROM "CustomerProfiles" WHERE "UserId" = NEW."CustomerId" FOR SHARE;
                    IF NOT FOUND OR customer."Id" IS NULL OR NOT customer."IsActive" OR
                       NOT customer."EmailConfirmed" OR customer."Role" <> 'Customer' THEN
                        RAISE EXCEPTION 'A test drive requires a current verified customer'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_TestDriveBookings_Customer';
                    END IF;
                    UPDATE "Branches" SET "Name" = "Name" WHERE "Id" = NEW."BranchId" AND "IsActive";
                    IF NOT FOUND THEN
                        RAISE EXCEPTION 'Test-drive branch is unavailable'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_TestDriveBookings_Eligibility';
                    END IF;
                    SELECT * INTO vehicle FROM "Vehicles" WHERE "Id" = NEW."VehicleId" AND "BranchId" = NEW."BranchId" FOR UPDATE;
                    SELECT * INTO publication FROM "ListingPublications" WHERE "Id" = NEW."PublicationId";
                    SELECT * INTO revision FROM "ListingRevisions" WHERE "Id" = publication."RevisionId";
                    IF vehicle."Id" IS NULL OR vehicle."Status" <> 'Available' OR publication."Id" IS NULL OR
                       publication."VehicleId" <> NEW."VehicleId" OR publication."BranchId" <> NEW."BranchId" OR
                       NEW."CreatedAt" < publication."RecordedAt" OR NEW."CreatedAt" > clock_timestamp() OR
                       NEW."StartsAt" <= clock_timestamp() OR
                       EXISTS (SELECT 1 FROM "Reservations" WHERE "VehicleId" = vehicle."Id" AND "IsActive") OR
                       EXISTS (SELECT 1 FROM "ListingWithdrawals" WHERE "PublicationId" = publication."Id") OR
                       EXISTS (SELECT 1 FROM "ListingPublications" p JOIN "ListingRevisions" r ON r."Id" = p."RevisionId"
                           WHERE p."VehicleId" = vehicle."Id" AND r."Version" > revision."Version") OR
                       NOT EXISTS (SELECT 1 FROM "InspectionSignedRevisions" s JOIN "InspectionDrafts" d ON d."Id" = s."InspectionId"
                           WHERE s."Id" = publication."SignedRevisionId" AND d."VehicleId" = vehicle."Id" AND
                               d."IsSigned" AND d."Revision" = s."Revision") THEN
                        RAISE EXCEPTION 'Test drive requires the current available publication'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_TestDriveBookings_Eligibility';
                    END IF;
                    today := date_trunc('day', clock_timestamp() AT TIME ZONE 'Asia/Kuala_Lumpur') AT TIME ZONE 'Asia/Kuala_Lumpur';
                    IF EXISTS (SELECT 1 FROM "TestDriveBookings" WHERE "CustomerId" = NEW."CustomerId" AND "StartsAt" >= today) THEN
                        RAISE EXCEPTION 'Customer already has an outstanding test drive'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_TestDriveBookings_CustomerLimit';
                    END IF;
                    IF EXISTS (SELECT 1 FROM "TestDriveBookings" WHERE "BranchId" = NEW."BranchId" AND
                        "StartsAt" < NEW."EndsAt" AND "EndsAt" > NEW."StartsAt") THEN
                        RAISE EXCEPTION 'Test-drive branch interval is occupied'
                            USING ERRCODE = '23P01', CONSTRAINT = 'EX_TestDriveBookings_Slot';
                    END IF;
                    -- Fence stale snapshots for both customer limits and competing vehicle availability writers.
                    UPDATE "AspNetUsers" SET "ConcurrencyStamp" = "ConcurrencyStamp" WHERE "Id" = customer."Id";
                    UPDATE "Vehicles" SET "Status" = "Status" WHERE "Id" = vehicle."Id";
                    RETURN NEW;
                END $$;

                CREATE OR REPLACE FUNCTION guard_test_drive_mail() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP = 'DELETE' OR (TG_OP = 'UPDATE' AND (
                        ROW(NEW."Id", NEW."BookingId", NEW."RecipientEmail") IS DISTINCT FROM
                        ROW(OLD."Id", OLD."BookingId", OLD."RecipientEmail") OR
                        (OLD."DeliveredAt" IS NOT NULL AND NEW."DeliveredAt" IS DISTINCT FROM OLD."DeliveredAt") OR
                        NEW."DeliveryAttempts" < OLD."DeliveryAttempts")) THEN
                        RAISE EXCEPTION 'Test-drive mail identity and first delivery receipt are immutable'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_TestDriveMailOutbox_Immutable';
                    END IF;
                    IF TG_OP = 'INSERT' AND NOT EXISTS (
                        SELECT 1 FROM "TestDriveBookings" b JOIN "AspNetUsers" u ON u."Id" = b."CustomerId"
                        WHERE b."Id" = NEW."BookingId" AND u."Email" = NEW."RecipientEmail") THEN
                        RAISE EXCEPTION 'Test-drive mail must retain the booking customer recipient'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_TestDriveMailOutbox_Recipient';
                    END IF;
                    RETURN NEW;
                END $$;

                CREATE OR REPLACE FUNCTION require_test_drive_confirmation() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM "TestDriveMailOutbox" WHERE "BookingId" = NEW."Id") THEN
                        RAISE EXCEPTION 'Test-drive booking requires its durable confirmation'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_TestDriveBookings_Confirmation';
                    END IF;
                    RETURN NULL;
                END $$;
                """);
        }
    }
}

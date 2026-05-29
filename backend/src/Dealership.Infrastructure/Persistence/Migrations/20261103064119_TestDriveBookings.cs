using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dealership.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TestDriveBookings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TestDriveBookings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    PublicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestDriveBookings", x => x.Id);
                    table.CheckConstraint("CK_TestDriveBookings_Shape", "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND isfinite(\"CreatedAt\") AND isfinite(\"StartsAt\") AND isfinite(\"EndsAt\") AND \"StartsAt\" > \"CreatedAt\" AND \"EndsAt\" = \"StartsAt\" + interval '30 minutes'");
                    table.ForeignKey(
                        name: "FK_TestDriveBookings_CustomerProfiles_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "CustomerProfiles",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TestDriveBookings_ListingPublications_PublicationId_Vehicle~",
                        columns: x => new { x.PublicationId, x.VehicleId, x.BranchId },
                        principalTable: "ListingPublications",
                        principalColumns: ["Id", "VehicleId", "BranchId"],
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TestDriveMailOutbox",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BookingId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    DeliveryAttempts = table.Column<int>(type: "integer", nullable: false),
                    NextDeliveryAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeliveryClaimId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeliveryClaimExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeliveredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestDriveMailOutbox", x => x.Id);
                    table.CheckConstraint("CK_TestDriveMailOutbox_Shape", "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"RecipientEmail\" ~ '[^[:space:]]' AND \"DeliveryAttempts\" >= 0 AND isfinite(\"NextDeliveryAt\") AND ((\"DeliveryClaimId\" IS NULL) = (\"DeliveryClaimExpiresAt\" IS NULL))");
                    table.ForeignKey(
                        name: "FK_TestDriveMailOutbox_TestDriveBookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "TestDriveBookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TestDriveBookings_BranchId_StartsAt_Id",
                table: "TestDriveBookings",
                columns: ["BranchId", "StartsAt", "Id"]);

            migrationBuilder.CreateIndex(
                name: "IX_TestDriveBookings_CustomerId_StartsAt_Id",
                table: "TestDriveBookings",
                columns: ["CustomerId", "StartsAt", "Id"]);

            migrationBuilder.CreateIndex(
                name: "IX_TestDriveBookings_PublicationId_VehicleId_BranchId",
                table: "TestDriveBookings",
                columns: ["PublicationId", "VehicleId", "BranchId"]);

            migrationBuilder.CreateIndex(
                name: "IX_TestDriveBookings_VehicleId_StartsAt_Id",
                table: "TestDriveBookings",
                columns: ["VehicleId", "StartsAt", "Id"]);

            migrationBuilder.CreateIndex(
                name: "IX_TestDriveMailOutbox_BookingId",
                table: "TestDriveMailOutbox",
                column: "BookingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TestDriveMailOutbox_NextDeliveryAt_Id",
                table: "TestDriveMailOutbox",
                columns: ["NextDeliveryAt", "Id"]);

            migrationBuilder.Sql("""
                CREATE FUNCTION guard_test_drive_booking() RETURNS trigger LANGUAGE plpgsql AS $$
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
                CREATE TRIGGER test_drive_booking_guard BEFORE INSERT OR UPDATE OR DELETE ON "TestDriveBookings"
                    FOR EACH ROW EXECUTE FUNCTION guard_test_drive_booking();

                CREATE FUNCTION guard_test_drive_mail() RETURNS trigger LANGUAGE plpgsql AS $$
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
                CREATE TRIGGER test_drive_mail_guard BEFORE INSERT OR UPDATE OR DELETE ON "TestDriveMailOutbox"
                    FOR EACH ROW EXECUTE FUNCTION guard_test_drive_mail();

                CREATE FUNCTION require_test_drive_confirmation() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM "TestDriveMailOutbox" WHERE "BookingId" = NEW."Id") THEN
                        RAISE EXCEPTION 'Test-drive booking requires its durable confirmation'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_TestDriveBookings_Confirmation';
                    END IF;
                    RETURN NULL;
                END $$;
                CREATE CONSTRAINT TRIGGER test_drive_confirmation_bundle AFTER INSERT ON "TestDriveBookings"
                    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION require_test_drive_confirmation();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER test_drive_confirmation_bundle ON "TestDriveBookings";
                DROP FUNCTION require_test_drive_confirmation();
                DROP TRIGGER test_drive_mail_guard ON "TestDriveMailOutbox";
                DROP FUNCTION guard_test_drive_mail();
                DROP TRIGGER test_drive_booking_guard ON "TestDriveBookings";
                DROP FUNCTION guard_test_drive_booking();
                """);
            migrationBuilder.DropTable(
                name: "TestDriveMailOutbox");

            migrationBuilder.DropTable(
                name: "TestDriveBookings");
        }
    }
}

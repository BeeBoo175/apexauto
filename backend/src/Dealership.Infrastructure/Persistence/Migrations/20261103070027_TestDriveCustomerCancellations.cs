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
            UpdateAdmission(migrationBuilder, restoring: false);
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
            UpdateAdmission(migrationBuilder, restoring: true);

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

        private static void UpdateAdmission(MigrationBuilder migrationBuilder, bool restoring)
        {
            var originalCustomer = "WHERE \"CustomerId\" = NEW.\"CustomerId\" AND \"StartsAt\" >= today";
            var currentCustomer = originalCustomer + " AND NOT EXISTS (SELECT 1 FROM \"TestDriveCancellations\" c WHERE c.\"BookingId\" = \"TestDriveBookings\".\"Id\")";
            var originalSlot = "\"StartsAt\" < NEW.\"EndsAt\" AND \"EndsAt\" > NEW.\"StartsAt\"";
            var currentSlot = originalSlot + " AND NOT EXISTS (SELECT 1 FROM \"TestDriveCancellations\" c WHERE c.\"BookingId\" = \"TestDriveBookings\".\"Id\")";
            ReplaceFunction(migrationBuilder, "guard_test_drive_booking()", originalCustomer, currentCustomer, restoring);
            ReplaceFunction(migrationBuilder, "guard_test_drive_booking()", originalSlot, currentSlot, restoring);
            ReplaceFunction(migrationBuilder, "guard_test_drive_mail()",
                "NEW.\"BookingId\", NEW.\"RecipientEmail\"", "NEW.\"BookingId\", NEW.\"RecipientEmail\", NEW.\"Kind\"", restoring);
            ReplaceFunction(migrationBuilder, "guard_test_drive_mail()",
                "OLD.\"BookingId\", OLD.\"RecipientEmail\"", "OLD.\"BookingId\", OLD.\"RecipientEmail\", OLD.\"Kind\"", restoring);
            ReplaceFunction(migrationBuilder, "require_test_drive_confirmation()",
                "WHERE \"BookingId\" = NEW.\"Id\"", "WHERE \"BookingId\" = NEW.\"Id\" AND \"Kind\" = 'Confirmation'", restoring);
        }

        private static void ReplaceFunction(MigrationBuilder migrationBuilder, string function, string original,
            string replacement, bool restoring)
        {
            var before = restoring ? replacement : original;
            var after = restoring ? original : replacement;
            migrationBuilder.Sql($"""
                DO $migration$
                DECLARE definition text := pg_get_functiondef('{function}'::regprocedure);
                BEGIN
                    IF (length(definition) - length(replace(definition, '{before.Replace("'", "''")}', ''))) /
                        length('{before.Replace("'", "''")}') <> 1 THEN
                        RAISE EXCEPTION 'Unexpected test-drive admission definition';
                    END IF;
                    EXECUTE replace(definition, '{before.Replace("'", "''")}', '{after.Replace("'", "''")}');
                END $migration$;
                """);
        }
    }
}

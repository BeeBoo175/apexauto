using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dealership.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TestDriveStaffCancellations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "TestDriveCancellations",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "CustomerCancelled");

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "TestDriveCancellations",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "StaffId",
                table: "TestDriveCancellations",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TestDriveCancellations_StaffId",
                table: "TestDriveCancellations",
                column: "StaffId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TestDriveCancellations_Actor",
                table: "TestDriveCancellations",
                sql: "(\"Kind\" = 'CustomerCancelled' AND \"StaffId\" IS NULL AND \"Reason\" IS NULL) OR (\"Kind\" = 'StaffCancelled' AND \"StaffId\" IS NOT NULL AND \"Reason\" IS NOT NULL AND length(\"Reason\") BETWEEN 1 AND 1000 AND \"Reason\" = btrim(\"Reason\") AND \"Reason\" ~ '[^[:space:]]' AND \"Reason\" !~ '[[:cntrl:]]')");

            migrationBuilder.AddForeignKey(
                name: "FK_TestDriveCancellations_AspNetUsers_StaffId",
                table: "TestDriveCancellations",
                column: "StaffId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql("ALTER TABLE \"TestDriveCancellations\" ALTER COLUMN \"Kind\" DROP DEFAULT;");
            UpdateGuard(migrationBuilder, restoring: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "TestDriveCancellations" WHERE "Kind" <> 'CustomerCancelled') THEN
                        RAISE EXCEPTION 'Retained staff cancellations prevent rollback';
                    END IF;
                END $$;
                """);
            UpdateGuard(migrationBuilder, restoring: true);

            migrationBuilder.DropForeignKey(
                name: "FK_TestDriveCancellations_AspNetUsers_StaffId",
                table: "TestDriveCancellations");

            migrationBuilder.DropIndex(
                name: "IX_TestDriveCancellations_StaffId",
                table: "TestDriveCancellations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TestDriveCancellations_Actor",
                table: "TestDriveCancellations");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "TestDriveCancellations");

            migrationBuilder.DropColumn(
                name: "Reason",
                table: "TestDriveCancellations");

            migrationBuilder.DropColumn(
                name: "StaffId",
                table: "TestDriveCancellations");
        }

        private static void UpdateGuard(MigrationBuilder migrationBuilder, bool restoring)
        {
            ReplaceGuard(migrationBuilder,
                "SELECT * INTO customer FROM \"AspNetUsers\" WHERE \"Id\" = NEW.\"CustomerId\" FOR UPDATE;",
                "SELECT * INTO customer FROM \"AspNetUsers\" WHERE \"Id\" = CASE WHEN NEW.\"Kind\" = 'StaffCancelled' THEN NEW.\"StaffId\" ELSE NEW.\"CustomerId\" END FOR UPDATE;",
                restoring);
            ReplaceGuard(migrationBuilder,
                "PERFORM 1 FROM \"CustomerProfiles\" WHERE \"UserId\" = NEW.\"CustomerId\" FOR SHARE;",
                """
                IF NEW."Kind" = 'CustomerCancelled' THEN
                    PERFORM 1 FROM "CustomerProfiles" WHERE "UserId" = NEW."CustomerId" FOR SHARE;
                ELSE
                    PERFORM 1 WHERE NEW."Kind" = 'StaffCancelled';
                END IF;
                """, restoring);
            ReplaceGuard(migrationBuilder, "customer.\"Role\" <> 'Customer'",
                "NOT ((NEW.\"Kind\" = 'CustomerCancelled' AND customer.\"Role\" = 'Customer') OR (NEW.\"Kind\" = 'StaffCancelled' AND customer.\"Role\" IN ('SalesAgent', 'BranchManager')))",
                restoring);
            ReplaceGuard(migrationBuilder,
                "UPDATE \"Vehicles\" SET \"Status\" = \"Status\" WHERE \"Id\" = booking.\"VehicleId\";",
                """
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
                """, restoring);
            ReplaceGuard(migrationBuilder,
                "NEW.\"RecordedAt\" >= booking.\"StartsAt\" OR clock_timestamp() >= booking.\"StartsAt\"",
                """
                (NEW."Kind" = 'CustomerCancelled' AND (NEW."RecordedAt" >= booking."StartsAt" OR clock_timestamp() >= booking."StartsAt")) OR
                       (NEW."Kind" = 'StaffCancelled' AND (
                           NEW."RecordedAt" >= ((booking."StartsAt" AT TIME ZONE 'Asia/Kuala_Lumpur')::date + 1)::timestamp AT TIME ZONE 'Asia/Kuala_Lumpur' OR
                           clock_timestamp() >= ((booking."StartsAt" AT TIME ZONE 'Asia/Kuala_Lumpur')::date + 1)::timestamp AT TIME ZONE 'Asia/Kuala_Lumpur')) OR
                       EXISTS (SELECT 1 FROM "TestDriveAssignments" WHERE "BookingId" = booking."Id" AND "RecordedAt" > NEW."RecordedAt")
                """, restoring);
        }

        private static void ReplaceGuard(MigrationBuilder migrationBuilder, string original, string replacement, bool restoring)
        {
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

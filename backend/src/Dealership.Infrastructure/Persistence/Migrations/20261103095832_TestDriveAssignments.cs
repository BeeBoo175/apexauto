using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dealership.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TestDriveAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_TestDriveBookings_Id_BranchId",
                table: "TestDriveBookings",
                columns: ["Id", "BranchId"]);

            migrationBuilder.CreateTable(
                name: "TestDriveAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BookingId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    HandlerId = table.Column<Guid>(type: "uuid", nullable: true),
                    PreviousHandlerId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExpectedVersion = table.Column<long>(type: "bigint", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestDriveAssignments", x => x.Id);
                    table.CheckConstraint("CK_TestDriveAssignments_Reason", "\"Reason\" IS NULL OR (length(\"Reason\") BETWEEN 1 AND 1000 AND \"Reason\" = btrim(\"Reason\") AND \"Reason\" ~ '[^[:space:]]' AND \"Reason\" !~ '[[:cntrl:]]')");
                    table.CheckConstraint("CK_TestDriveAssignments_Shape", "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"ExpectedVersion\" >= 0 AND \"Version\" > 0 AND \"Version\" - 1 = \"ExpectedVersion\" AND isfinite(\"RecordedAt\")");
                    table.CheckConstraint("CK_TestDriveAssignments_Transition", "(\"Kind\" = 'Claimed' AND \"HandlerId\" = \"ActorId\" AND \"HandlerId\" IS NOT NULL AND \"PreviousHandlerId\" IS NULL AND \"Reason\" IS NULL) OR (\"Kind\" = 'Assigned' AND \"HandlerId\" IS NOT NULL AND \"HandlerId\" IS DISTINCT FROM \"PreviousHandlerId\" AND (\"PreviousHandlerId\" IS NULL OR \"Reason\" IS NOT NULL)) OR (\"Kind\" = 'Released' AND \"HandlerId\" IS NULL AND \"PreviousHandlerId\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_TestDriveAssignments_AspNetUsers_ActorId",
                        column: x => x.ActorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TestDriveAssignments_AspNetUsers_HandlerId",
                        column: x => x.HandlerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TestDriveAssignments_AspNetUsers_PreviousHandlerId",
                        column: x => x.PreviousHandlerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TestDriveAssignments_TestDriveBookings_BookingId_BranchId",
                        columns: x => new { x.BookingId, x.BranchId },
                        principalTable: "TestDriveBookings",
                        principalColumns: ["Id", "BranchId"],
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TestDriveAssignments_ActorId",
                table: "TestDriveAssignments",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_TestDriveAssignments_BookingId_BranchId",
                table: "TestDriveAssignments",
                columns: ["BookingId", "BranchId"]);

            migrationBuilder.CreateIndex(
                name: "IX_TestDriveAssignments_BookingId_Version",
                table: "TestDriveAssignments",
                columns: ["BookingId", "Version"],
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TestDriveAssignments_HandlerId_BookingId",
                table: "TestDriveAssignments",
                columns: ["HandlerId", "BookingId"]);

            migrationBuilder.CreateIndex(
                name: "IX_TestDriveAssignments_PreviousHandlerId",
                table: "TestDriveAssignments",
                column: "PreviousHandlerId");
            migrationBuilder.Sql("""
                CREATE FUNCTION public.staff_schedule_overlaps(handler uuid, starts_at timestamptz,
                    ends_at timestamptz, excluded_appointment uuid, excluded_booking uuid) RETURNS boolean
                LANGUAGE sql STABLE SET search_path = pg_catalog AS $$
                    SELECT EXISTS (
                        SELECT 1 FROM public."IntakeAppointments" a
                        JOIN LATERAL (SELECT h."HandlerId" FROM public."AppointmentAssignments" h
                            WHERE h."AppointmentId" = a."Id" ORDER BY h."Version" DESC LIMIT 1) current ON true
                        WHERE a."Id" IS DISTINCT FROM excluded_appointment AND current."HandlerId" = handler AND
                            a."CancelledAt" IS NULL AND a."StartsAt" < ends_at AND starts_at < a."EndsAt" AND
                            NOT EXISTS (SELECT 1 FROM public."AppointmentCheckIns" c WHERE c."AppointmentId" = a."Id")
                    ) OR EXISTS (
                        SELECT 1 FROM public."TestDriveBookings" b
                        JOIN LATERAL (SELECT h."HandlerId" FROM public."TestDriveAssignments" h
                            WHERE h."BookingId" = b."Id" ORDER BY h."Version" DESC LIMIT 1) current ON true
                        WHERE b."Id" IS DISTINCT FROM excluded_booking AND current."HandlerId" = handler AND
                            b."StartsAt" < ends_at AND starts_at < b."EndsAt" AND NOT EXISTS (
                                SELECT 1 FROM public."TestDriveCancellations" c WHERE c."BookingId" = b."Id")
                    );
                $$;
                CREATE FUNCTION public.guard_test_drive_assignment() RETURNS trigger
                LANGUAGE plpgsql SET search_path = pg_catalog AS $$
                DECLARE
                    booking public."TestDriveBookings"%ROWTYPE;
                    previous public."TestDriveAssignments"%ROWTYPE;
                    actor public."AspNetUsers"%ROWTYPE;
                BEGIN
                    IF TG_OP <> 'INSERT' THEN
                        RAISE EXCEPTION 'Test-drive ownership history is immutable'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_TestDriveAssignments_Immutable';
                    END IF;
                    PERFORM pg_advisory_xact_lock(hashtextextended('test-drive-assignment:' || NEW."Id"::text, 0));
                    PERFORM 1 FROM public."AspNetUsers" WHERE "Id" IN (NEW."ActorId", NEW."HandlerId")
                        ORDER BY "Id" FOR SHARE;
                    SELECT * INTO actor FROM public."AspNetUsers" WHERE "Id" = NEW."ActorId";
                    -- Shared branch writes fence stale assignment, cancellation and check-in snapshots.
                    UPDATE public."Branches" SET "IsActive" = "IsActive" WHERE "Id" = NEW."BranchId";
                    IF NOT EXISTS (SELECT 1 FROM public."Branches" WHERE "Id" = NEW."BranchId" AND "IsActive") OR
                        actor."Id" IS NULL OR NOT actor."IsActive" OR
                        actor."BranchId" IS DISTINCT FROM NEW."BranchId" OR
                        actor."Role" NOT IN ('SalesAgent', 'BranchManager') OR
                        (NEW."Kind" = 'Assigned' AND actor."Role" <> 'BranchManager') OR
                        (NEW."HandlerId" IS NOT NULL AND NOT EXISTS (
                            SELECT 1 FROM public."AspNetUsers" WHERE "Id" = NEW."HandlerId" AND "IsActive" AND
                                "BranchId" = NEW."BranchId" AND "Role" IN ('SalesAgent', 'BranchManager'))) THEN
                        RAISE EXCEPTION 'Ownership requires current eligible branch staff'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_TestDriveAssignments_Eligibility';
                    END IF;
                    SELECT * INTO booking FROM public."TestDriveBookings" WHERE "Id" = NEW."BookingId";
                    IF booking."Id" IS NULL OR booking."BranchId" <> NEW."BranchId" OR
                        EXISTS (SELECT 1 FROM public."TestDriveCancellations" WHERE "BookingId" = booking."Id") OR NEW."RecordedAt" < booking."CreatedAt" OR
                        NEW."RecordedAt" > clock_timestamp() OR
                        clock_timestamp() >= ((booking."StartsAt" AT TIME ZONE 'Asia/Kuala_Lumpur')::date + 1)::timestamp
                            AT TIME ZONE 'Asia/Kuala_Lumpur' THEN
                        RAISE EXCEPTION 'Only unfinished test drives accept ownership changes'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_TestDriveAssignments_Availability';
                    END IF;
                    UPDATE public."Vehicles" SET "Status" = "Status" WHERE "Id" = booking."VehicleId";
                    SELECT * INTO previous FROM public."TestDriveAssignments" WHERE "BookingId" = NEW."BookingId"
                        ORDER BY "Version" DESC LIMIT 1;
                    IF NEW."ExpectedVersion" <> coalesce(previous."Version", 0) OR
                        NEW."PreviousHandlerId" IS DISTINCT FROM previous."HandlerId" OR
                        NEW."RecordedAt" < previous."RecordedAt" THEN
                        RAISE EXCEPTION 'Ownership must extend the exact current history'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_TestDriveAssignments_Chain';
                    END IF;
                    IF NEW."Kind" = 'Released' AND actor."Role" = 'SalesAgent' AND
                        NEW."PreviousHandlerId" IS DISTINCT FROM NEW."ActorId" THEN
                        RAISE EXCEPTION 'Agents release only their own unfinished work'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_TestDriveAssignments_Eligibility';
                    END IF;
                    IF NEW."HandlerId" IS NOT NULL AND public.staff_schedule_overlaps(
                        NEW."HandlerId", booking."StartsAt", booking."EndsAt", NULL, booking."Id") THEN
                        RAISE EXCEPTION 'Handler already has overlapping booked work'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_TestDriveAssignments_Overlap';
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER test_drive_assignment_guard BEFORE INSERT OR UPDATE OR DELETE ON public."TestDriveAssignments"
                    FOR EACH ROW EXECUTE FUNCTION public.guard_test_drive_assignment();
                """);
            InstallIntakeAdmission(migrationBuilder);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "TestDriveAssignments") THEN
                        RAISE EXCEPTION 'Retained test-drive ownership prevents rollback';
                    END IF;
                END $$;
                DROP TRIGGER test_drive_assignment_guard ON "TestDriveAssignments";
                DROP FUNCTION public.guard_test_drive_assignment();
                """);
            RestoreIntakeAdmission(migrationBuilder);
            migrationBuilder.Sql("DROP FUNCTION public.staff_schedule_overlaps(uuid, timestamptz, timestamptz, uuid, uuid);");
            migrationBuilder.DropTable(
                name: "TestDriveAssignments");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_TestDriveBookings_Id_BranchId",
                table: "TestDriveBookings");
        }

        private static void InstallIntakeAdmission(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION public.guard_appointment_assignment() RETURNS trigger
                LANGUAGE plpgsql SET search_path = pg_catalog AS $$
                DECLARE
                    appointment public."IntakeAppointments"%ROWTYPE;
                    previous public."AppointmentAssignments"%ROWTYPE;
                    actor public."AspNetUsers"%ROWTYPE;
                BEGIN
                    IF TG_OP <> 'INSERT' THEN
                        RAISE EXCEPTION 'Intake ownership history is immutable'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_AppointmentAssignments_Immutable';
                    END IF;
                    PERFORM 1 FROM public."AspNetUsers" WHERE "Id" IN (NEW."ActorId", NEW."HandlerId")
                        ORDER BY "Id" FOR SHARE;
                    SELECT * INTO actor FROM public."AspNetUsers" WHERE "Id" = NEW."ActorId";
                    -- Shared branch writes fence stale assignment, cancellation and check-in snapshots.
                    UPDATE public."Branches" SET "IsActive" = "IsActive" WHERE "Id" = NEW."BranchId";
                    IF NOT EXISTS (SELECT 1 FROM public."Branches" WHERE "Id" = NEW."BranchId" AND "IsActive") OR
                        actor."Id" IS NULL OR NOT actor."IsActive" OR
                        actor."BranchId" IS DISTINCT FROM NEW."BranchId" OR
                        actor."Role" NOT IN ('SalesAgent', 'BranchManager') OR
                        (NEW."Kind" = 'Assigned' AND actor."Role" <> 'BranchManager') OR
                        (NEW."HandlerId" IS NOT NULL AND NOT EXISTS (
                            SELECT 1 FROM public."AspNetUsers" WHERE "Id" = NEW."HandlerId" AND "IsActive" AND
                                "BranchId" = NEW."BranchId" AND "Role" IN ('SalesAgent', 'BranchManager'))) THEN
                        RAISE EXCEPTION 'Ownership requires current eligible branch staff'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_AppointmentAssignments_Eligibility';
                    END IF;
                    SELECT * INTO appointment FROM public."IntakeAppointments" WHERE "Id" = NEW."AppointmentId";
                    IF appointment."Id" IS NULL OR appointment."BranchId" <> NEW."BranchId" OR
                        appointment."CancelledAt" IS NOT NULL OR NEW."RecordedAt" < appointment."CreatedAt" OR
                        NEW."RecordedAt" > clock_timestamp() OR
                        clock_timestamp() >= ((appointment."StartsAt" AT TIME ZONE 'Asia/Kuala_Lumpur')::date + 1)::timestamp
                            AT TIME ZONE 'Asia/Kuala_Lumpur' OR
                        EXISTS (SELECT 1 FROM public."AppointmentCheckIns" WHERE "AppointmentId" = NEW."AppointmentId") THEN
                        RAISE EXCEPTION 'Only unfinished booked appointments accept ownership changes'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_AppointmentAssignments_Availability';
                    END IF;
                    SELECT * INTO previous FROM public."AppointmentAssignments" WHERE "AppointmentId" = NEW."AppointmentId"
                        ORDER BY "Version" DESC LIMIT 1;
                    IF NEW."ExpectedVersion" <> coalesce(previous."Version", 0) OR
                        NEW."PreviousHandlerId" IS DISTINCT FROM previous."HandlerId" OR
                        NEW."RecordedAt" < previous."RecordedAt" THEN
                        RAISE EXCEPTION 'Ownership must extend the exact current history'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_AppointmentAssignments_Chain';
                    END IF;
                    IF NEW."Kind" = 'Released' AND actor."Role" = 'SalesAgent' AND
                        NEW."PreviousHandlerId" IS DISTINCT FROM NEW."ActorId" THEN
                        RAISE EXCEPTION 'Agents release only their own unfinished work'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_AppointmentAssignments_Eligibility';
                    END IF;
                    IF NEW."HandlerId" IS NOT NULL AND public.staff_schedule_overlaps(NEW."HandlerId", appointment."StartsAt", appointment."EndsAt", appointment."Id", NULL) THEN
                        RAISE EXCEPTION 'Handler already has overlapping booked work'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_AppointmentAssignments_Overlap';
                    END IF;
                    RETURN NEW;
                END $$;
                """);
        }

        private static void RestoreIntakeAdmission(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION public.guard_appointment_assignment() RETURNS trigger
                LANGUAGE plpgsql SET search_path = pg_catalog AS $$
                DECLARE
                    appointment public."IntakeAppointments"%ROWTYPE;
                    previous public."AppointmentAssignments"%ROWTYPE;
                    actor public."AspNetUsers"%ROWTYPE;
                BEGIN
                    IF TG_OP <> 'INSERT' THEN
                        RAISE EXCEPTION 'Intake ownership history is immutable'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_AppointmentAssignments_Immutable';
                    END IF;
                    PERFORM 1 FROM public."AspNetUsers" WHERE "Id" IN (NEW."ActorId", NEW."HandlerId")
                        ORDER BY "Id" FOR SHARE;
                    SELECT * INTO actor FROM public."AspNetUsers" WHERE "Id" = NEW."ActorId";
                    -- Shared branch writes fence stale assignment, cancellation and check-in snapshots.
                    UPDATE public."Branches" SET "IsActive" = "IsActive" WHERE "Id" = NEW."BranchId";
                    IF NOT EXISTS (SELECT 1 FROM public."Branches" WHERE "Id" = NEW."BranchId" AND "IsActive") OR
                        actor."Id" IS NULL OR NOT actor."IsActive" OR
                        actor."BranchId" IS DISTINCT FROM NEW."BranchId" OR
                        actor."Role" NOT IN ('SalesAgent', 'BranchManager') OR
                        (NEW."Kind" = 'Assigned' AND actor."Role" <> 'BranchManager') OR
                        (NEW."HandlerId" IS NOT NULL AND NOT EXISTS (
                            SELECT 1 FROM public."AspNetUsers" WHERE "Id" = NEW."HandlerId" AND "IsActive" AND
                                "BranchId" = NEW."BranchId" AND "Role" IN ('SalesAgent', 'BranchManager'))) THEN
                        RAISE EXCEPTION 'Ownership requires current eligible branch staff'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_AppointmentAssignments_Eligibility';
                    END IF;
                    SELECT * INTO appointment FROM public."IntakeAppointments" WHERE "Id" = NEW."AppointmentId";
                    IF appointment."Id" IS NULL OR appointment."BranchId" <> NEW."BranchId" OR
                        appointment."CancelledAt" IS NOT NULL OR NEW."RecordedAt" < appointment."CreatedAt" OR
                        NEW."RecordedAt" > clock_timestamp() OR
                        clock_timestamp() >= ((appointment."StartsAt" AT TIME ZONE 'Asia/Kuala_Lumpur')::date + 1)::timestamp
                            AT TIME ZONE 'Asia/Kuala_Lumpur' OR
                        EXISTS (SELECT 1 FROM public."AppointmentCheckIns" WHERE "AppointmentId" = NEW."AppointmentId") THEN
                        RAISE EXCEPTION 'Only unfinished booked appointments accept ownership changes'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_AppointmentAssignments_Availability';
                    END IF;
                    SELECT * INTO previous FROM public."AppointmentAssignments" WHERE "AppointmentId" = NEW."AppointmentId"
                        ORDER BY "Version" DESC LIMIT 1;
                    IF NEW."ExpectedVersion" <> coalesce(previous."Version", 0) OR
                        NEW."PreviousHandlerId" IS DISTINCT FROM previous."HandlerId" OR
                        NEW."RecordedAt" < previous."RecordedAt" THEN
                        RAISE EXCEPTION 'Ownership must extend the exact current history'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_AppointmentAssignments_Chain';
                    END IF;
                    IF NEW."Kind" = 'Released' AND actor."Role" = 'SalesAgent' AND
                        NEW."PreviousHandlerId" IS DISTINCT FROM NEW."ActorId" THEN
                        RAISE EXCEPTION 'Agents release only their own unfinished work'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_AppointmentAssignments_Eligibility';
                    END IF;
                    IF NEW."HandlerId" IS NOT NULL AND EXISTS (
                        SELECT 1 FROM public."IntakeAppointments" a
                        JOIN LATERAL (SELECT h."HandlerId" FROM public."AppointmentAssignments" h
                            WHERE h."AppointmentId" = a."Id" ORDER BY h."Version" DESC LIMIT 1) current ON true
                        WHERE a."Id" <> appointment."Id" AND current."HandlerId" = NEW."HandlerId" AND
                            a."CancelledAt" IS NULL AND a."StartsAt" < appointment."EndsAt" AND
                            appointment."StartsAt" < a."EndsAt" AND NOT EXISTS (
                                SELECT 1 FROM public."AppointmentCheckIns" c WHERE c."AppointmentId" = a."Id")) THEN
                        RAISE EXCEPTION 'Handler already has overlapping booked work'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_AppointmentAssignments_Overlap';
                    END IF;
                    RETURN NEW;
                END $$;
                """);
        }
    }
}

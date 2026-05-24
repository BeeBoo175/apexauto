using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dealership.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AppointmentAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_IntakeAppointments_Id_BranchId",
                table: "IntakeAppointments",
                columns: ["Id", "BranchId"]);

            migrationBuilder.CreateTable(
                name: "AppointmentAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AppointmentId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_AppointmentAssignments", x => x.Id);
                    table.CheckConstraint("CK_AppointmentAssignments_Reason", "\"Reason\" IS NULL OR (length(\"Reason\") BETWEEN 1 AND 1000 AND \"Reason\" = btrim(\"Reason\") AND \"Reason\" ~ '[^[:space:]]' AND \"Reason\" !~ '[[:cntrl:]]')");
                    table.CheckConstraint("CK_AppointmentAssignments_Shape", "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"ExpectedVersion\" >= 0 AND \"Version\" > 0 AND \"Version\" - 1 = \"ExpectedVersion\" AND isfinite(\"RecordedAt\")");
                    table.CheckConstraint("CK_AppointmentAssignments_Transition", "(\"Kind\" = 'Claimed' AND \"HandlerId\" = \"ActorId\" AND \"HandlerId\" IS NOT NULL AND \"PreviousHandlerId\" IS NULL AND \"Reason\" IS NULL) OR (\"Kind\" = 'Assigned' AND \"HandlerId\" IS NOT NULL AND \"HandlerId\" IS DISTINCT FROM \"PreviousHandlerId\" AND (\"PreviousHandlerId\" IS NULL OR \"Reason\" IS NOT NULL)) OR (\"Kind\" = 'Released' AND \"HandlerId\" IS NULL AND \"PreviousHandlerId\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_AppointmentAssignments_AspNetUsers_ActorId",
                        column: x => x.ActorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppointmentAssignments_AspNetUsers_HandlerId",
                        column: x => x.HandlerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppointmentAssignments_AspNetUsers_PreviousHandlerId",
                        column: x => x.PreviousHandlerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppointmentAssignments_IntakeAppointments_AppointmentId_Bra~",
                        columns: x => new { x.AppointmentId, x.BranchId },
                        principalTable: "IntakeAppointments",
                        principalColumns: ["Id", "BranchId"],
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentAssignments_ActorId",
                table: "AppointmentAssignments",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentAssignments_AppointmentId_BranchId",
                table: "AppointmentAssignments",
                columns: ["AppointmentId", "BranchId"]);

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentAssignments_AppointmentId_Version",
                table: "AppointmentAssignments",
                columns: ["AppointmentId", "Version"],
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentAssignments_HandlerId_AppointmentId",
                table: "AppointmentAssignments",
                columns: ["HandlerId", "AppointmentId"]);

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentAssignments_PreviousHandlerId",
                table: "AppointmentAssignments",
                column: "PreviousHandlerId");

            migrationBuilder.Sql("""
                CREATE FUNCTION public.guard_appointment_assignment() RETURNS trigger
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
                CREATE TRIGGER appointment_assignment_guard BEFORE INSERT OR UPDATE OR DELETE ON public."AppointmentAssignments"
                    FOR EACH ROW EXECUTE FUNCTION public.guard_appointment_assignment();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION public.guard_appointment_assignment() CASCADE;");
            migrationBuilder.DropTable(
                name: "AppointmentAssignments");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_IntakeAppointments_Id_BranchId",
                table: "IntakeAppointments");
        }
    }
}

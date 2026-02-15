using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dealership.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InspectionAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_Vehicles_Id_BranchId",
                table: "Vehicles",
                columns: ["Id", "BranchId"]);

            migrationBuilder.CreateTable(
                name: "InspectionDrafts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    TechnicianId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AssignmentVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InspectionDrafts", x => x.Id);
                    table.UniqueConstraint("AK_InspectionDrafts_Id_BranchId", x => new { x.Id, x.BranchId });
                    table.CheckConstraint("CK_InspectionDrafts_Shape", "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"AssignmentVersion\" > 0");
                    table.ForeignKey(
                        name: "FK_InspectionDrafts_AspNetUsers_TechnicianId",
                        column: x => x.TechnicianId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InspectionDrafts_Vehicles_VehicleId_BranchId",
                        columns: x => new { x.VehicleId, x.BranchId },
                        principalTable: "Vehicles",
                        principalColumns: ["Id", "BranchId"],
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InspectionAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InspectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    TechnicianId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreviousTechnicianId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExpectedVersion = table.Column<long>(type: "bigint", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InspectionAssignments", x => x.Id);
                    table.CheckConstraint("CK_InspectionAssignments_Reason", "(\"ExpectedVersion\" = 0 AND \"PreviousTechnicianId\" IS NULL AND \"Reason\" IS NULL AND \"ActorId\" = \"TechnicianId\") OR (\"ExpectedVersion\" > 0 AND \"PreviousTechnicianId\" IS NOT NULL AND \"PreviousTechnicianId\" <> \"TechnicianId\" AND \"Reason\" IS NOT NULL AND \"Reason\" ~ '[^[:space:]]' AND \"Reason\" !~ '[[:cntrl:]]')");
                    table.CheckConstraint("CK_InspectionAssignments_Shape", "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"ExpectedVersion\" >= 0 AND \"Version\" > 0 AND \"Version\" - 1 = \"ExpectedVersion\"");
                    table.ForeignKey(
                        name: "FK_InspectionAssignments_AspNetUsers_ActorId",
                        column: x => x.ActorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InspectionAssignments_AspNetUsers_PreviousTechnicianId",
                        column: x => x.PreviousTechnicianId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InspectionAssignments_AspNetUsers_TechnicianId",
                        column: x => x.TechnicianId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InspectionAssignments_InspectionDrafts_InspectionId_BranchId",
                        columns: x => new { x.InspectionId, x.BranchId },
                        principalTable: "InspectionDrafts",
                        principalColumns: ["Id", "BranchId"],
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InspectionAssignments_ActorId",
                table: "InspectionAssignments",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionAssignments_InspectionId_BranchId",
                table: "InspectionAssignments",
                columns: ["InspectionId", "BranchId"]);

            migrationBuilder.CreateIndex(
                name: "IX_InspectionAssignments_InspectionId_Version",
                table: "InspectionAssignments",
                columns: ["InspectionId", "Version"],
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InspectionAssignments_PreviousTechnicianId",
                table: "InspectionAssignments",
                column: "PreviousTechnicianId");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionAssignments_TechnicianId",
                table: "InspectionAssignments",
                column: "TechnicianId");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionDrafts_BranchId_CreatedAt_Id",
                table: "InspectionDrafts",
                columns: ["BranchId", "CreatedAt", "Id"]);

            migrationBuilder.CreateIndex(
                name: "IX_InspectionDrafts_TechnicianId",
                table: "InspectionDrafts",
                column: "TechnicianId");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionDrafts_VehicleId",
                table: "InspectionDrafts",
                column: "VehicleId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InspectionDrafts_VehicleId_BranchId",
                table: "InspectionDrafts",
                columns: ["VehicleId", "BranchId"]);

            migrationBuilder.Sql("""
                CREATE FUNCTION guard_inspection_draft() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        RAISE EXCEPTION 'Inspection draft provenance is retained'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionDrafts_Immutable';
                    END IF;
                    IF TG_OP = 'UPDATE' THEN
                        IF (NEW."Id", NEW."VehicleId", NEW."BranchId", NEW."CreatedAt") IS DISTINCT FROM
                           (OLD."Id", OLD."VehicleId", OLD."BranchId", OLD."CreatedAt") OR
                           NEW."AssignmentVersion" - 1 <> OLD."AssignmentVersion" OR
                           NEW."TechnicianId" = OLD."TechnicianId" THEN
                            RAISE EXCEPTION 'Only a consecutive recorded reassignment may change a draft'
                                USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionDrafts_Immutable';
                        END IF;
                    ELSIF NEW."AssignmentVersion" <> 1 THEN
                        RAISE EXCEPTION 'Initial claim requires version one'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionDrafts_Version';
                    END IF;
                    PERFORM 1 FROM "Vehicles" WHERE "Id" = NEW."VehicleId" AND "BranchId" = NEW."BranchId"
                        AND "Status" = 'PendingInspection' FOR UPDATE;
                    IF NOT FOUND THEN
                        RAISE EXCEPTION 'Inspection requires a matching pending vehicle'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionDrafts_Vehicle';
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER inspection_draft_guard BEFORE INSERT OR UPDATE OR DELETE ON "InspectionDrafts"
                    FOR EACH ROW EXECUTE FUNCTION guard_inspection_draft();

                CREATE FUNCTION guard_inspection_assignment() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE
                    draft "InspectionDrafts"%ROWTYPE;
                    previous "InspectionAssignments"%ROWTYPE;
                    actor "AspNetUsers"%ROWTYPE;
                    technician "AspNetUsers"%ROWTYPE;
                BEGIN
                    IF TG_OP <> 'INSERT' THEN
                        RAISE EXCEPTION 'Inspection assignment receipts are immutable'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionAssignments_Immutable';
                    END IF;
                    PERFORM 1 FROM "AspNetUsers" WHERE "Id" IN (NEW."ActorId", NEW."TechnicianId")
                        ORDER BY "Id" FOR SHARE;
                    SELECT * INTO actor FROM "AspNetUsers" WHERE "Id" = NEW."ActorId";
                    SELECT * INTO technician FROM "AspNetUsers" WHERE "Id" = NEW."TechnicianId";
                    PERFORM 1 FROM "Branches" WHERE "Id" = NEW."BranchId" AND "IsActive" FOR SHARE;
                    IF NOT FOUND OR actor."Id" IS NULL OR technician."Id" IS NULL OR
                       NOT actor."IsActive" OR NOT technician."IsActive" OR
                       actor."BranchId" IS DISTINCT FROM NEW."BranchId" OR
                       technician."BranchId" IS DISTINCT FROM NEW."BranchId" OR
                       technician."Role" <> 'WorkshopTechnician' OR
                       (NEW."ExpectedVersion" = 0 AND actor."Role" <> 'WorkshopTechnician') OR
                       (NEW."ExpectedVersion" > 0 AND actor."Role" <> 'BranchManager') THEN
                        RAISE EXCEPTION 'Assignment requires current active branch roles'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionAssignments_Eligibility';
                    END IF;
                    SELECT * INTO draft FROM "InspectionDrafts" WHERE "Id" = NEW."InspectionId" FOR UPDATE;
                    IF draft."Id" IS NULL OR draft."BranchId" <> NEW."BranchId" OR
                       draft."TechnicianId" <> NEW."TechnicianId" OR draft."AssignmentVersion" <> NEW."Version" OR
                       NEW."RecordedAt" < draft."CreatedAt" THEN
                        RAISE EXCEPTION 'Assignment must match its draft'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionAssignments_Draft';
                    END IF;
                    IF NEW."ExpectedVersion" = 0 THEN
                        IF NEW."Id" <> draft."Id" OR NEW."RecordedAt" <> draft."CreatedAt" THEN
                            RAISE EXCEPTION 'Claim identity and timestamp must match'
                                USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionAssignments_Chain';
                        END IF;
                    ELSE
                        SELECT * INTO previous FROM "InspectionAssignments" WHERE
                            "InspectionId" = NEW."InspectionId" AND "Version" = NEW."ExpectedVersion";
                        IF previous."Id" IS NULL OR previous."TechnicianId" IS DISTINCT FROM NEW."PreviousTechnicianId" OR
                           NEW."RecordedAt" < previous."RecordedAt" THEN
                            RAISE EXCEPTION 'Reassignment must extend the retained previous assignment'
                                USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionAssignments_Chain';
                        END IF;
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER inspection_assignment_guard BEFORE INSERT OR UPDATE OR DELETE ON "InspectionAssignments"
                    FOR EACH ROW EXECUTE FUNCTION guard_inspection_assignment();

                CREATE FUNCTION require_inspection_assignment() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM "InspectionDrafts" d JOIN "InspectionAssignments" a ON a."InspectionId" = d."Id"
                        WHERE d."Id" = NEW."Id" AND a."BranchId" = d."BranchId" AND
                            a."TechnicianId" = d."TechnicianId" AND a."Version" = d."AssignmentVersion"
                    ) THEN
                        RAISE EXCEPTION 'Inspection draft requires its exact assignment receipt'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionDrafts_Receipt';
                    END IF;
                    RETURN NULL;
                END $$;
                CREATE CONSTRAINT TRIGGER inspection_draft_receipt AFTER INSERT OR UPDATE ON "InspectionDrafts"
                    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION require_inspection_assignment();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InspectionAssignments");

            migrationBuilder.DropTable(
                name: "InspectionDrafts");

            migrationBuilder.Sql("""
                DROP FUNCTION require_inspection_assignment();
                DROP FUNCTION guard_inspection_assignment();
                DROP FUNCTION guard_inspection_draft();
                """);

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Vehicles_Id_BranchId",
                table: "Vehicles");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dealership.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InspectionRepairRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_InspectionDrafts_Shape",
                table: "InspectionDrafts");

            migrationBuilder.AddColumn<long>(
                name: "RepairVersion",
                table: "InspectionDrafts",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.Sql("""
                CREATE FUNCTION valid_inspection_repair_items(items jsonb) RETURNS boolean LANGUAGE plpgsql IMMUTABLE AS $$
                DECLARE
                    item jsonb;
                    codes text[] := ARRAY[]::text[];
                    code text;
                BEGIN
                    IF jsonb_typeof(items) IS DISTINCT FROM 'array' THEN
                        RETURN false;
                    END IF;
                    IF jsonb_array_length(items) NOT BETWEEN 1 AND 210 OR octet_length(items::text) > 2097152 THEN
                        RETURN false;
                    END IF;
                    FOR item IN SELECT value FROM jsonb_array_elements(items) LOOP
                        IF jsonb_typeof(item) IS DISTINCT FROM 'object' THEN
                            RETURN false;
                        END IF;
                        IF NOT item ?& ARRAY['pointCode', 'work'] OR (SELECT count(*) FROM jsonb_object_keys(item)) <> 2 OR
                           jsonb_typeof(item->'pointCode') IS DISTINCT FROM 'string' OR
                           jsonb_typeof(item->'work') IS DISTINCT FROM 'string' THEN
                            RETURN false;
                        END IF;
                        code := item->>'pointCode';
                        IF code = '' OR code = ANY(codes) OR
                           (SELECT coalesce(sum(CASE WHEN ascii(character) > 65535 THEN 2 ELSE 1 END), 0)
                               FROM regexp_split_to_table(item->>'work', '') AS character) > 1000 OR
                           translate(item->>'work', (SELECT string_agg(chr(value), '') FROM unnest(
                               ARRAY[9,10,11,12,13,32,133,160,5760,8192,8193,8194,8195,8196,8197,8198,8199,8200,8201,8202,8232,8233,8239,8287,12288]) AS value), '') = '' OR
                           EXISTS (SELECT 1 FROM regexp_split_to_table(item->>'work', '') AS character
                               WHERE ascii(character) < 32 OR ascii(character) BETWEEN 127 AND 159) THEN
                            RETURN false;
                        END IF;
                        codes := array_append(codes, code);
                    END LOOP;
                    RETURN true;
                END $$;
                """);
            migrationBuilder.CreateTable(
                name: "InspectionRepairRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InspectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    SignedRevisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignmentVersion = table.Column<long>(type: "bigint", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    ExpectedVersion = table.Column<long>(type: "bigint", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    ItemsJson = table.Column<string>(type: "jsonb", nullable: false),
                    Budget = table.Column<decimal>(type: "numeric", nullable: false),
                    RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InspectionRepairRequests", x => x.Id);
                    table.CheckConstraint("CK_InspectionRepairRequests_Budget", "\"Budget\" <= 9999999999999999.99 AND \"Budget\" = trunc(\"Budget\", 2)");
                    table.CheckConstraint("CK_InspectionRepairRequests_Items", "valid_inspection_repair_items(\"ItemsJson\")");
                    table.CheckConstraint("CK_InspectionRepairRequests_Shape", "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"AssignmentVersion\" > 0 AND \"Revision\" > 0 AND \"ExpectedVersion\" >= 0 AND \"Version\" > 0 AND \"Version\" - 1 = \"ExpectedVersion\" AND \"Budget\" >= 0 AND isfinite(\"RequestedAt\")");
                    table.ForeignKey(
                        name: "FK_InspectionRepairRequests_AspNetUsers_ActorId",
                        column: x => x.ActorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InspectionRepairRequests_InspectionDrafts_InspectionId_Bran~",
                        columns: x => new { x.InspectionId, x.BranchId },
                        principalTable: "InspectionDrafts",
                        principalColumns: ["Id", "BranchId"],
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InspectionRepairRequests_InspectionSignedRevisions_SignedRe~",
                        column: x => x.SignedRevisionId,
                        principalTable: "InspectionSignedRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_InspectionDrafts_Shape",
                table: "InspectionDrafts",
                sql: "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"AssignmentVersion\" > 0 AND \"Revision\" > 0 AND \"ReopenedAfterSaveVersion\" >= 0 AND \"RepairVersion\" >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionRepairRequests_ActorId",
                table: "InspectionRepairRequests",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionRepairRequests_BranchId_RequestedAt_Id",
                table: "InspectionRepairRequests",
                columns: ["BranchId", "RequestedAt", "Id"]);

            migrationBuilder.CreateIndex(
                name: "IX_InspectionRepairRequests_InspectionId_BranchId",
                table: "InspectionRepairRequests",
                columns: ["InspectionId", "BranchId"]);

            migrationBuilder.CreateIndex(
                name: "IX_InspectionRepairRequests_InspectionId_Version",
                table: "InspectionRepairRequests",
                columns: ["InspectionId", "Version"],
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InspectionRepairRequests_SignedRevisionId_Version",
                table: "InspectionRepairRequests",
                columns: ["SignedRevisionId", "Version"]);
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION guard_inspection_draft() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        RAISE EXCEPTION 'Inspection draft provenance is retained'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionDrafts_Immutable';
                    END IF;
                    IF TG_OP = 'UPDATE' THEN
                        IF OLD."IsSigned" AND NEW."IsSigned" AND NEW."RepairVersion" - 1 = OLD."RepairVersion" AND
                           (to_jsonb(NEW) - 'RepairVersion') = (to_jsonb(OLD) - 'RepairVersion') THEN
                            RETURN NEW;
                        END IF;
                        IF NOT OLD."IsSigned" AND NEW."IsSigned" AND
                           (to_jsonb(NEW) - 'IsSigned') = (to_jsonb(OLD) - 'IsSigned') THEN
                            RETURN NEW;
                        END IF;
                        IF OLD."IsSigned" AND NOT NEW."IsSigned" AND NEW."Revision" - 1 = OLD."Revision" AND
                           (to_jsonb(NEW) - ARRAY['IsSigned', 'Revision', 'ReopenedAfterSaveVersion']) =
                           (to_jsonb(OLD) - ARRAY['IsSigned', 'Revision', 'ReopenedAfterSaveVersion']) THEN
                            RETURN NEW;
                        END IF;
                        IF (NEW."Id", NEW."VehicleId", NEW."BranchId", NEW."CreatedAt", NEW."Revision", NEW."ReopenedAfterSaveVersion", NEW."RepairVersion") IS DISTINCT FROM
                           (OLD."Id", OLD."VehicleId", OLD."BranchId", OLD."CreatedAt", OLD."Revision", OLD."ReopenedAfterSaveVersion", OLD."RepairVersion") OR
                           NEW."AssignmentVersion" - 1 <> OLD."AssignmentVersion" OR
                           NEW."TechnicianId" = OLD."TechnicianId" OR OLD."IsSigned" OR NEW."IsSigned" THEN
                            RAISE EXCEPTION 'Only an unsigned recorded reassignment or reasoned reopening may change a draft'
                                USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionDrafts_Immutable';
                        END IF;
                    ELSIF NEW."AssignmentVersion" <> 1 OR NEW."Revision" <> 1 OR NEW."IsSigned" OR NEW."ReopenedAfterSaveVersion" <> 0 OR NEW."RepairVersion" <> 0 THEN
                        RAISE EXCEPTION 'Initial claim requires unsigned revision one'
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
                CREATE FUNCTION guard_inspection_repair_request() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE
                    actor "AspNetUsers"%ROWTYPE;
                    draft "InspectionDrafts"%ROWTYPE;
                    signed "InspectionSignedRevisions"%ROWTYPE;
                    vehicle_id uuid;
                BEGIN
                    IF TG_OP <> 'INSERT' THEN
                        RAISE EXCEPTION 'Repair requests are retained immutable evidence'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionRepairRequests_Immutable';
                    END IF;
                    SELECT * INTO actor FROM "AspNetUsers" WHERE "Id" = NEW."ActorId" FOR SHARE;
                    PERFORM 1 FROM "Branches" WHERE "Id" = NEW."BranchId" AND "IsActive" FOR SHARE;
                    IF NOT FOUND OR actor."Id" IS NULL OR NOT actor."IsActive" OR
                       actor."Role" <> 'WorkshopTechnician' OR actor."BranchId" IS DISTINCT FROM NEW."BranchId" THEN
                        RAISE EXCEPTION 'Repair requests require a current active branch technician'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionRepairRequests_Eligibility';
                    END IF;
                    SELECT "VehicleId" INTO vehicle_id FROM "InspectionDrafts" WHERE "Id" = NEW."InspectionId";
                    PERFORM 1 FROM "Vehicles" WHERE "Id" = vehicle_id AND "BranchId" = NEW."BranchId"
                        AND "Status" = 'PendingInspection' FOR UPDATE;
                    IF NOT FOUND THEN
                        RAISE EXCEPTION 'Repair requests require an eligible matching vehicle'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionRepairRequests_Vehicle';
                    END IF;
                    SELECT * INTO draft FROM "InspectionDrafts" WHERE "Id" = NEW."InspectionId" FOR UPDATE;
                    SELECT * INTO signed FROM "InspectionSignedRevisions" WHERE "Id" = NEW."SignedRevisionId";
                    IF draft."Id" IS NULL OR NOT draft."IsSigned" OR draft."BranchId" <> NEW."BranchId" OR
                       draft."TechnicianId" <> NEW."ActorId" OR draft."AssignmentVersion" <> NEW."AssignmentVersion" OR
                       draft."Revision" <> NEW."Revision" OR draft."RepairVersion" <> NEW."ExpectedVersion" OR
                       signed."Id" IS NULL OR signed."InspectionId" <> draft."Id" OR signed."Revision" <> draft."Revision" OR
                       signed."ReportJson"->'assessment'->'isRejected' IS DISTINCT FROM 'false'::jsonb OR
                       NEW."RequestedAt" < signed."SignedAt" OR NEW."RequestedAt" > clock_timestamp() THEN
                        RAISE EXCEPTION 'Repair request must select the current non-rejected signed revision and repair version'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionRepairRequests_Revision';
                    END IF;
                    IF NOT valid_inspection_repair_items(NEW."ItemsJson") THEN
                        RAISE EXCEPTION 'Repair work requires a bounded unique finding scope'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionRepairRequests_Items';
                    END IF;
                    IF EXISTS (SELECT 1 FROM jsonb_array_elements(NEW."ItemsJson") item WHERE NOT EXISTS (
                        SELECT 1 FROM jsonb_array_elements(signed."ReportJson"->'checklist') point
                        WHERE point->>'code' = item->>'pointCode' AND point->'kind' = '0'::jsonb)) THEN
                        RAISE EXCEPTION 'Repair scope must reference findings in its selected signed report'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionRepairRequests_Findings';
                    END IF;
                    UPDATE "InspectionDrafts" SET "RepairVersion" = NEW."Version" WHERE "Id" = draft."Id";
                    RETURN NEW;
                END $$;
                CREATE TRIGGER inspection_repair_request_guard BEFORE INSERT OR UPDATE OR DELETE ON "InspectionRepairRequests"
                    FOR EACH ROW EXECUTE FUNCTION guard_inspection_repair_request();

                CREATE FUNCTION require_inspection_repair_request() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "InspectionDrafts" d WHERE d."Id" = NEW."Id" AND d."RepairVersion" > 0 AND
                        NOT EXISTS (SELECT 1 FROM "InspectionRepairRequests" r
                            WHERE r."InspectionId" = d."Id" AND r."Version" = d."RepairVersion")) THEN
                        RAISE EXCEPTION 'Repair version requires its retained exact request'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionDrafts_RepairRequest';
                    END IF;
                    RETURN NULL;
                END $$;
                CREATE CONSTRAINT TRIGGER inspection_draft_repair_request AFTER INSERT OR UPDATE ON "InspectionDrafts"
                    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION require_inspection_repair_request();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER inspection_draft_repair_request ON "InspectionDrafts";
                DROP FUNCTION require_inspection_repair_request();
                DROP TRIGGER inspection_repair_request_guard ON "InspectionRepairRequests";
                DROP FUNCTION guard_inspection_repair_request();
                CREATE OR REPLACE FUNCTION guard_inspection_draft() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        RAISE EXCEPTION 'Inspection draft provenance is retained'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionDrafts_Immutable';
                    END IF;
                    IF TG_OP = 'UPDATE' THEN
                        IF NOT OLD."IsSigned" AND NEW."IsSigned" AND
                           (to_jsonb(NEW) - 'IsSigned') = (to_jsonb(OLD) - 'IsSigned') THEN
                            RETURN NEW;
                        END IF;
                        IF OLD."IsSigned" AND NOT NEW."IsSigned" AND NEW."Revision" - 1 = OLD."Revision" AND
                           (to_jsonb(NEW) - ARRAY['IsSigned', 'Revision', 'ReopenedAfterSaveVersion']) =
                           (to_jsonb(OLD) - ARRAY['IsSigned', 'Revision', 'ReopenedAfterSaveVersion']) THEN
                            RETURN NEW;
                        END IF;
                        IF (NEW."Id", NEW."VehicleId", NEW."BranchId", NEW."CreatedAt", NEW."Revision", NEW."ReopenedAfterSaveVersion") IS DISTINCT FROM
                           (OLD."Id", OLD."VehicleId", OLD."BranchId", OLD."CreatedAt", OLD."Revision", OLD."ReopenedAfterSaveVersion") OR
                           NEW."AssignmentVersion" - 1 <> OLD."AssignmentVersion" OR
                           NEW."TechnicianId" = OLD."TechnicianId" OR OLD."IsSigned" OR NEW."IsSigned" THEN
                            RAISE EXCEPTION 'Only an unsigned recorded reassignment or reasoned reopening may change a draft'
                                USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionDrafts_Immutable';
                        END IF;
                    ELSIF NEW."AssignmentVersion" <> 1 OR NEW."Revision" <> 1 OR NEW."IsSigned" OR NEW."ReopenedAfterSaveVersion" <> 0 THEN
                        RAISE EXCEPTION 'Initial claim requires unsigned revision one'
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
                """);
            migrationBuilder.DropTable(
                name: "InspectionRepairRequests");
            migrationBuilder.Sql("DROP FUNCTION valid_inspection_repair_items(jsonb);");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InspectionDrafts_Shape",
                table: "InspectionDrafts");

            migrationBuilder.DropColumn(
                name: "RepairVersion",
                table: "InspectionDrafts");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InspectionDrafts_Shape",
                table: "InspectionDrafts",
                sql: "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"AssignmentVersion\" > 0 AND \"Revision\" > 0 AND \"ReopenedAfterSaveVersion\" >= 0");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dealership.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InspectionDraftSaves : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InspectionDraftSaves",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InspectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignmentVersion = table.Column<long>(type: "bigint", nullable: false),
                    ExpectedVersion = table.Column<long>(type: "bigint", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CatalogVersion = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    EntriesJson = table.Column<string>(type: "jsonb", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InspectionDraftSaves", x => x.Id);
                    table.CheckConstraint("CK_InspectionDraftSaves_Shape", "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"AssignmentVersion\" > 0 AND \"ExpectedVersion\" >= 0 AND \"Version\" > 0 AND \"Version\" - 1 = \"ExpectedVersion\"");
                    table.ForeignKey(
                        name: "FK_InspectionDraftSaves_AspNetUsers_ActorId",
                        column: x => x.ActorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InspectionDraftSaves_InspectionDrafts_InspectionId_BranchId",
                        columns: x => new { x.InspectionId, x.BranchId },
                        principalTable: "InspectionDrafts",
                        principalColumns: ["Id", "BranchId"],
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InspectionDraftSaves_ActorId",
                table: "InspectionDraftSaves",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionDraftSaves_InspectionId_BranchId",
                table: "InspectionDraftSaves",
                columns: ["InspectionId", "BranchId"]);

            migrationBuilder.CreateIndex(
                name: "IX_InspectionDraftSaves_InspectionId_Version",
                table: "InspectionDraftSaves",
                columns: ["InspectionId", "Version"],
                unique: true);

            migrationBuilder.Sql("""
                CREATE FUNCTION valid_inspection_draft_entries(entries jsonb) RETURNS boolean
                LANGUAGE plpgsql IMMUTABLE AS $$
                DECLARE
                    entry jsonb;
                    code text;
                    codes text[] := ARRAY[]::text[];
                    point_limit integer;
                    amount numeric;
                    total numeric := 0;
                BEGIN
                    IF jsonb_typeof(entries) IS DISTINCT FROM 'array' THEN
                        RETURN false;
                    END IF;
                    -- Allow UTF-8 and JSONB expansion of every bounded, domain-valid exception set.
                    IF jsonb_array_length(entries) > 210 OR octet_length(entries::text) > 2097152 THEN
                        RETURN false;
                    END IF;
                    FOR entry IN SELECT value FROM jsonb_array_elements(entries) LOOP
                        IF jsonb_typeof(entry) IS DISTINCT FROM 'object' THEN
                            RETURN false;
                        END IF;
                        IF NOT entry ?& ARRAY['pointCode', 'kind', 'severity', 'estimatedRepairCost', 'notes'] OR
                           (SELECT count(*) FROM jsonb_object_keys(entry)) <> 5 OR
                           jsonb_typeof(entry->'pointCode') IS DISTINCT FROM 'string' OR
                           jsonb_typeof(entry->'notes') IS DISTINCT FROM 'string' OR
                           jsonb_typeof(entry->'kind') IS DISTINCT FROM 'number' OR
                           jsonb_typeof(entry->'estimatedRepairCost') IS DISTINCT FROM 'number' THEN
                            RETURN false;
                        END IF;
                        code := entry->>'pointCode';
                        IF code !~ '^(FC|ET|EP|IE|UB)-[0-9]{3}$' OR code = ANY(codes) THEN
                            RETURN false;
                        END IF;
                        point_limit := CASE left(code, 2) WHEN 'FC' THEN 45 WHEN 'ET' THEN 50
                            WHEN 'EP' THEN 45 WHEN 'IE' THEN 40 WHEN 'UB' THEN 30 END;
                        -- Count UTF-16 units to match the Domain note limit, including supplementary characters.
                        IF right(code, 3)::integer NOT BETWEEN 1 AND point_limit OR
                           entry->>'kind' NOT IN ('0', '1') OR
                           (SELECT coalesce(sum(CASE WHEN ascii(character) > 65535 THEN 2 ELSE 1 END), 0)
                               FROM regexp_split_to_table(entry->>'notes', '') AS character) > 1000 OR
                           translate(entry->>'notes', (SELECT string_agg(chr(value), '') FROM unnest(
                               ARRAY[9,10,11,12,13,32,133,160,5760,8192,8193,8194,8195,8196,8197,8198,8199,8200,8201,8202,8232,8233,8239,8287,12288]) AS value), '') = '' OR
                           EXISTS (SELECT 1 FROM regexp_split_to_table(entry->>'notes', '') AS character
                               WHERE ascii(character) < 32 OR ascii(character) BETWEEN 127 AND 159) THEN
                            RETURN false;
                        END IF;
                        codes := array_append(codes, code);
                        amount := trim_scale((entry->>'estimatedRepairCost')::numeric);
                        IF amount < 0 OR scale(amount) > 2 OR
                           amount * power(10::numeric, scale(amount)) > 79228162514264337593543950335 THEN
                            RETURN false;
                        END IF;
                        IF entry->>'kind' = '1' THEN
                            IF entry->'severity' <> 'null'::jsonb OR amount <> 0 OR
                               code NOT IN ('ET-045', 'ET-046', 'EP-034', 'EP-042',
                                   'IE-027', 'IE-029', 'IE-033', 'IE-037', 'IE-040') THEN
                                RETURN false;
                            END IF;
                        ELSE
                            IF jsonb_typeof(entry->'severity') IS DISTINCT FROM 'number' OR
                               entry->>'severity' NOT IN ('0', '1', '2', '3') THEN
                                RETURN false;
                            END IF;
                            total := trim_scale(total + amount);
                            IF total * power(10::numeric, scale(total)) > 79228162514264337593543950335 THEN
                                RETURN false;
                            END IF;
                        END IF;
                    END LOOP;
                    RETURN total * power(10::numeric, scale(total)) <= 79228162514264337593543950335;
                END $$;

                CREATE FUNCTION guard_inspection_draft_save() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE
                    draft "InspectionDrafts"%ROWTYPE;
                    actor "AspNetUsers"%ROWTYPE;
                    previous "InspectionDraftSaves"%ROWTYPE;
                    vehicle_id uuid;
                BEGIN
                    IF TG_OP <> 'INSERT' THEN
                        RAISE EXCEPTION 'Inspection saves and receipts are immutable'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionDraftSaves_Immutable';
                    END IF;
                    SELECT * INTO actor FROM "AspNetUsers" WHERE "Id" = NEW."ActorId" FOR SHARE;
                    PERFORM 1 FROM "Branches" WHERE "Id" = NEW."BranchId" AND "IsActive" FOR SHARE;
                    IF NOT FOUND OR actor."Id" IS NULL OR NOT actor."IsActive" OR
                       actor."Role" <> 'WorkshopTechnician' OR actor."BranchId" IS DISTINCT FROM NEW."BranchId" THEN
                        RAISE EXCEPTION 'Save requires a current active branch technician'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionDraftSaves_Eligibility';
                    END IF;
                    SELECT "VehicleId" INTO vehicle_id FROM "InspectionDrafts" WHERE "Id" = NEW."InspectionId";
                    PERFORM 1 FROM "Vehicles" WHERE "Id" = vehicle_id AND "Status" = 'PendingInspection' FOR UPDATE;
                    IF NOT FOUND THEN
                        RAISE EXCEPTION 'Save requires a pending vehicle'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionDraftSaves_Vehicle';
                    END IF;
                    SELECT * INTO draft FROM "InspectionDrafts" WHERE "Id" = NEW."InspectionId" FOR UPDATE;
                    IF draft."BranchId" <> NEW."BranchId" OR draft."TechnicianId" <> NEW."ActorId" OR
                       draft."AssignmentVersion" <> NEW."AssignmentVersion" THEN
                        RAISE EXCEPTION 'Save requires the current assigned technician and assignment version'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionDraftSaves_Assignment';
                    END IF;
                    SELECT * INTO previous FROM "InspectionDraftSaves" WHERE "InspectionId" = NEW."InspectionId"
                        ORDER BY "Version" DESC LIMIT 1;
                    IF NOT isfinite(NEW."RecordedAt") OR NEW."ExpectedVersion" <> coalesce(previous."Version", 0) OR
                       NEW."RecordedAt" < greatest(draft."CreatedAt", previous."RecordedAt") THEN
                        RAISE EXCEPTION 'Save must extend the current draft version'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionDraftSaves_Version';
                    END IF;
                    IF NEW."CatalogVersion" <> 'apex-inspection-1' OR NOT valid_inspection_draft_entries(NEW."EntriesJson") THEN
                        RAISE EXCEPTION 'Save requires unique valid catalog exceptions and exact MYR estimates'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionDraftSaves_Entries';
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER inspection_draft_save_guard BEFORE INSERT OR UPDATE OR DELETE ON "InspectionDraftSaves"
                    FOR EACH ROW EXECUTE FUNCTION guard_inspection_draft_save();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InspectionDraftSaves");

            migrationBuilder.Sql("""
                DROP FUNCTION guard_inspection_draft_save();
                DROP FUNCTION valid_inspection_draft_entries(jsonb);
                """);
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dealership.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InspectionReopenings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_InspectionDrafts_Shape",
                table: "InspectionDrafts");

            migrationBuilder.AddColumn<long>(
                name: "ReopenedAfterSaveVersion",
                table: "InspectionDrafts",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "InspectionReopenings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InspectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    SignedRevisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignmentVersion = table.Column<long>(type: "bigint", nullable: false),
                    ExpectedRevision = table.Column<long>(type: "bigint", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    AfterSaveVersion = table.Column<long>(type: "bigint", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InspectionReopenings", x => x.Id);
                    table.CheckConstraint("CK_InspectionReopenings_Reason", "\"Reason\" ~ '[^[:space:]]' AND \"Reason\" !~ '[[:cntrl:]]' AND length(\"Reason\") <= 1000");
                    table.CheckConstraint("CK_InspectionReopenings_Shape", "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"AssignmentVersion\" > 0 AND \"ExpectedRevision\" > 0 AND \"Revision\" > 1 AND \"Revision\" - 1 = \"ExpectedRevision\" AND \"AfterSaveVersion\" > 0 AND isfinite(\"RecordedAt\")");
                    table.ForeignKey(
                        name: "FK_InspectionReopenings_AspNetUsers_ActorId",
                        column: x => x.ActorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InspectionReopenings_InspectionDrafts_InspectionId_BranchId",
                        columns: x => new { x.InspectionId, x.BranchId },
                        principalTable: "InspectionDrafts",
                        principalColumns: ["Id", "BranchId"],
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InspectionReopenings_InspectionSignedRevisions_SignedRevisi~",
                        column: x => x.SignedRevisionId,
                        principalTable: "InspectionSignedRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_InspectionDrafts_Shape",
                table: "InspectionDrafts",
                sql: "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"AssignmentVersion\" > 0 AND \"Revision\" > 0 AND \"ReopenedAfterSaveVersion\" >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionReopenings_ActorId",
                table: "InspectionReopenings",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionReopenings_InspectionId_BranchId",
                table: "InspectionReopenings",
                columns: ["InspectionId", "BranchId"]);

            migrationBuilder.CreateIndex(
                name: "IX_InspectionReopenings_InspectionId_Revision",
                table: "InspectionReopenings",
                columns: ["InspectionId", "Revision"],
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InspectionReopenings_SignedRevisionId",
                table: "InspectionReopenings",
                column: "SignedRevisionId",
                unique: true);

            migrationBuilder.Sql("""
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

                CREATE FUNCTION guard_inspection_reopening() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE
                    actor "AspNetUsers"%ROWTYPE;
                    draft "InspectionDrafts"%ROWTYPE;
                    signed "InspectionSignedRevisions"%ROWTYPE;
                    vehicle_id uuid;
                    latest_version bigint;
                BEGIN
                    IF TG_OP <> 'INSERT' THEN
                        RAISE EXCEPTION 'Inspection reopening receipts are immutable'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionReopenings_Immutable';
                    END IF;
                    SELECT * INTO actor FROM "AspNetUsers" WHERE "Id" = NEW."ActorId" FOR SHARE;
                    PERFORM 1 FROM "Branches" WHERE "Id" = NEW."BranchId" AND "IsActive" FOR SHARE;
                    IF NOT FOUND OR actor."Id" IS NULL OR NOT actor."IsActive" OR
                       actor."Role" <> 'BranchManager' OR actor."BranchId" IS DISTINCT FROM NEW."BranchId" THEN
                        RAISE EXCEPTION 'Reopening requires a current active branch manager'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionReopenings_Eligibility';
                    END IF;
                    SELECT "VehicleId" INTO vehicle_id FROM "InspectionDrafts" WHERE "Id" = NEW."InspectionId";
                    PERFORM 1 FROM "Vehicles" WHERE "Id" = vehicle_id AND "BranchId" = NEW."BranchId"
                        AND "Status" = 'PendingInspection' FOR UPDATE;
                    IF NOT FOUND THEN
                        RAISE EXCEPTION 'Reopening requires a matching eligible vehicle'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionReopenings_Vehicle';
                    END IF;
                    SELECT * INTO draft FROM "InspectionDrafts" WHERE "Id" = NEW."InspectionId" FOR UPDATE;
                    SELECT * INTO signed FROM "InspectionSignedRevisions" WHERE "Id" = NEW."SignedRevisionId";
                    SELECT max("Version") INTO latest_version FROM "InspectionDraftSaves" WHERE "InspectionId" = NEW."InspectionId";
                    IF draft."Id" IS NULL OR NOT draft."IsSigned" OR draft."BranchId" <> NEW."BranchId" OR
                       draft."Revision" <> NEW."ExpectedRevision" OR draft."AssignmentVersion" <> NEW."AssignmentVersion" OR
                       signed."Id" IS NULL OR signed."InspectionId" <> draft."Id" OR signed."Revision" <> draft."Revision" OR
                       NEW."AfterSaveVersion" IS DISTINCT FROM latest_version OR
                       NEW."RecordedAt" < signed."SignedAt" OR NEW."RecordedAt" > clock_timestamp() THEN
                        RAISE EXCEPTION 'Reopening must retain the exact current signed revision and save boundary'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionReopenings_Revision';
                    END IF;
                    UPDATE "InspectionDrafts" SET "IsSigned" = false, "Revision" = NEW."Revision",
                        "ReopenedAfterSaveVersion" = NEW."AfterSaveVersion" WHERE "Id" = draft."Id";
                    RETURN NEW;
                END $$;
                CREATE TRIGGER inspection_reopening_guard BEFORE INSERT OR UPDATE OR DELETE ON "InspectionReopenings"
                    FOR EACH ROW EXECUTE FUNCTION guard_inspection_reopening();

                CREATE FUNCTION require_inspection_reopening() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "InspectionDrafts" d WHERE d."Id" = NEW."Id" AND
                        ((d."Revision" = 1 AND d."ReopenedAfterSaveVersion" <> 0) OR
                         (d."Revision" > 1 AND NOT EXISTS (SELECT 1 FROM "InspectionReopenings" r
                            WHERE r."InspectionId" = d."Id" AND r."Revision" = d."Revision" AND
                                r."AfterSaveVersion" = d."ReopenedAfterSaveVersion")))) THEN
                        RAISE EXCEPTION 'A new revision requires its exact retained manager receipt'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionDrafts_Reopening';
                    END IF;
                    RETURN NULL;
                END $$;
                CREATE CONSTRAINT TRIGGER inspection_draft_reopening AFTER INSERT OR UPDATE ON "InspectionDrafts"
                    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION require_inspection_reopening();

                CREATE FUNCTION require_inspection_revision_save() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    PERFORM 1 FROM "InspectionDrafts" WHERE "Id" = NEW."InspectionId" AND
                        NEW."SaveVersion" <= "ReopenedAfterSaveVersion";
                    IF FOUND THEN
                        RAISE EXCEPTION 'A reopened revision requires a new fully attested save'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionSignedRevisions_FreshSave';
                    END IF;
                    RETURN NEW;
                END $$;
                -- Run after the source/seal guard has taken actor, vehicle and draft locks, before uniqueness checks.
                CREATE TRIGGER inspection_signed_revision_save_guard BEFORE INSERT ON "InspectionSignedRevisions"
                    FOR EACH ROW EXECUTE FUNCTION require_inspection_revision_save();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER inspection_signed_revision_save_guard ON "InspectionSignedRevisions";
                DROP FUNCTION require_inspection_revision_save();
                DROP TRIGGER inspection_draft_reopening ON "InspectionDrafts";
                DROP FUNCTION require_inspection_reopening();
                DROP TRIGGER inspection_reopening_guard ON "InspectionReopenings";
                DROP FUNCTION guard_inspection_reopening();
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
                        IF (NEW."Id", NEW."VehicleId", NEW."BranchId", NEW."CreatedAt", NEW."Revision") IS DISTINCT FROM
                           (OLD."Id", OLD."VehicleId", OLD."BranchId", OLD."CreatedAt", OLD."Revision") OR
                           NEW."AssignmentVersion" - 1 <> OLD."AssignmentVersion" OR
                           NEW."TechnicianId" = OLD."TechnicianId" OR OLD."IsSigned" OR NEW."IsSigned" THEN
                            RAISE EXCEPTION 'Only an unsigned consecutive recorded reassignment may change a draft'
                                USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionDrafts_Immutable';
                        END IF;
                    ELSIF NEW."AssignmentVersion" <> 1 OR NEW."Revision" <> 1 OR NEW."IsSigned" THEN
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
                name: "InspectionReopenings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InspectionDrafts_Shape",
                table: "InspectionDrafts");

            migrationBuilder.DropColumn(
                name: "ReopenedAfterSaveVersion",
                table: "InspectionDrafts");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InspectionDrafts_Shape",
                table: "InspectionDrafts",
                sql: "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"AssignmentVersion\" > 0 AND \"Revision\" > 0");
        }
    }
}

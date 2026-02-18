using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dealership.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InspectionPhotoAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PhotoIdsJson",
                table: "InspectionDraftSaves",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_InspectionPhotos_Id_InspectionId_PointCode",
                table: "InspectionPhotos",
                columns: ["Id", "InspectionId", "PointCode"]);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_InspectionDraftSaves_Id_InspectionId",
                table: "InspectionDraftSaves",
                columns: ["Id", "InspectionId"]);

            migrationBuilder.CreateTable(
                name: "InspectionDraftPhotos",
                columns: table => new
                {
                    SaveId = table.Column<Guid>(type: "uuid", nullable: false),
                    PhotoId = table.Column<Guid>(type: "uuid", nullable: false),
                    InspectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PointCode = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InspectionDraftPhotos", x => new { x.SaveId, x.PhotoId });
                    table.ForeignKey(
                        name: "FK_InspectionDraftPhotos_InspectionDraftSaves_SaveId_Inspectio~",
                        columns: x => new { x.SaveId, x.InspectionId },
                        principalTable: "InspectionDraftSaves",
                        principalColumns: ["Id", "InspectionId"],
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InspectionDraftPhotos_InspectionPhotos_PhotoId_InspectionId~",
                        columns: x => new { x.PhotoId, x.InspectionId, x.PointCode },
                        principalTable: "InspectionPhotos",
                        principalColumns: ["Id", "InspectionId", "PointCode"],
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InspectionPhotoRemovals",
                columns: table => new
                {
                    PhotoId = table.Column<Guid>(type: "uuid", nullable: false),
                    EligibleAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ClaimId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClaimExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InspectionPhotoRemovals", x => x.PhotoId);
                    table.CheckConstraint("CK_InspectionPhotoRemovals_Shape", "isfinite(\"EligibleAt\") AND isfinite(\"RequestedAt\") AND \"RequestedAt\" >= \"EligibleAt\" AND \"Attempts\" >= 0 AND isfinite(\"NextAttemptAt\") AND (\"DeletedAt\" IS NULL OR (isfinite(\"DeletedAt\") AND \"DeletedAt\" >= \"RequestedAt\")) AND ((\"ClaimId\" IS NULL) = (\"ClaimExpiresAt\" IS NULL)) AND (\"ClaimId\" IS NULL OR (\"ClaimId\" <> '00000000-0000-0000-0000-000000000000'::uuid AND isfinite(\"ClaimExpiresAt\")))");
                    table.ForeignKey(
                        name: "FK_InspectionPhotoRemovals_InspectionPhotos_PhotoId",
                        column: x => x.PhotoId,
                        principalTable: "InspectionPhotos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InspectionDraftPhotos_PhotoId_InspectionId_PointCode",
                table: "InspectionDraftPhotos",
                columns: ["PhotoId", "InspectionId", "PointCode"]);

            migrationBuilder.CreateIndex(
                name: "IX_InspectionDraftPhotos_SaveId_InspectionId",
                table: "InspectionDraftPhotos",
                columns: ["SaveId", "InspectionId"]);

            migrationBuilder.CreateIndex(
                name: "IX_InspectionPhotoRemovals_NextAttemptAt_PhotoId",
                table: "InspectionPhotoRemovals",
                columns: ["NextAttemptAt", "PhotoId"]);

            migrationBuilder.Sql("""
                CREATE FUNCTION require_inspection_save_photos() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF jsonb_typeof(NEW."PhotoIdsJson") IS DISTINCT FROM 'array' THEN
                        RAISE EXCEPTION 'Photo identities must be an array'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionDraftPhotos_Complete';
                    END IF;
                    IF jsonb_array_length(NEW."PhotoIdsJson") > 840 OR EXISTS
                        (SELECT 1 FROM jsonb_array_elements(NEW."PhotoIdsJson") value WHERE
                         jsonb_typeof(value) <> 'string' OR trim(both '"' from value::text) !~
                            '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' OR
                         value = '"00000000-0000-0000-0000-000000000000"'::jsonb) THEN
                        RAISE EXCEPTION 'Photo identities must be valid bounded UUIDs'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionDraftPhotos_Complete';
                    END IF;
                    IF (SELECT count(DISTINCT value) FROM jsonb_array_elements(NEW."PhotoIdsJson")) <>
                        jsonb_array_length(NEW."PhotoIdsJson") OR
                       (SELECT count(*) FROM "InspectionDraftPhotos" WHERE "SaveId" = NEW."Id") <>
                        jsonb_array_length(NEW."PhotoIdsJson") OR EXISTS
                       (SELECT 1 FROM jsonb_array_elements_text(NEW."PhotoIdsJson") requested WHERE NOT EXISTS
                        (SELECT 1 FROM "InspectionDraftPhotos" WHERE "SaveId" = NEW."Id" AND "PhotoId" = requested::uuid)) THEN
                        RAISE EXCEPTION 'Each declared photo requires its exact retained attachment at commit'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionDraftPhotos_Complete';
                    END IF;
                    RETURN NULL;
                END $$;
                CREATE CONSTRAINT TRIGGER inspection_save_photos_complete AFTER INSERT ON "InspectionDraftSaves"
                    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION require_inspection_save_photos();

                CREATE FUNCTION guard_inspection_draft_photo() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE
                    photo "InspectionPhotos"%ROWTYPE;
                    saved "InspectionDraftSaves"%ROWTYPE;
                BEGIN
                    IF TG_OP <> 'INSERT' THEN
                        RAISE EXCEPTION 'Saved photo references are immutable'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionDraftPhotos_Immutable';
                    END IF;
                    SELECT * INTO saved FROM "InspectionDraftSaves" WHERE "Id" = NEW."SaveId" FOR UPDATE;
                    UPDATE "InspectionPhotos" SET "AvailableAt" = "AvailableAt" WHERE "Id" = NEW."PhotoId"
                        RETURNING * INTO photo;
                    IF photo."Id" IS NULL OR saved."Id" IS NULL THEN
                        RETURN NEW;
                    END IF;
                    IF NOT saved."PhotoIdsJson" @> to_jsonb(NEW."PhotoId"::text) OR NOT EXISTS
                        (SELECT 1 FROM jsonb_array_elements(saved."EntriesJson") entry
                         WHERE entry->>'pointCode' = NEW."PointCode" AND entry->>'kind' = '0') OR
                       (SELECT count(*) FROM "InspectionDraftPhotos" WHERE "SaveId" = NEW."SaveId"
                            AND "PointCode" = NEW."PointCode") >= 4 THEN
                        RAISE EXCEPTION 'Photos must be declared and belong to a finding with at most four images'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionDraftPhotos_Finding';
                    END IF;
                    IF photo."AvailableAt" IS NULL OR photo."AvailableAt" > clock_timestamp() OR EXISTS
                        (SELECT 1 FROM "InspectionPhotoRemovals" WHERE "PhotoId" = NEW."PhotoId") OR
                       (photo."ExpiresAt" <= clock_timestamp() AND NOT EXISTS
                        (SELECT 1 FROM "InspectionDraftPhotos" WHERE "PhotoId" = NEW."PhotoId")) THEN
                        RAISE EXCEPTION 'Only available retained or unexpired photos may be attached'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionDraftPhotos_Available';
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER inspection_draft_photo_guard BEFORE INSERT OR UPDATE OR DELETE ON "InspectionDraftPhotos"
                    FOR EACH ROW EXECUTE FUNCTION guard_inspection_draft_photo();

                CREATE FUNCTION guard_inspection_photo_removal() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE photo "InspectionPhotos"%ROWTYPE;
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        RAISE EXCEPTION 'Inspection cleanup history is retained'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionPhotoRemovals_Immutable';
                    END IF;
                    IF TG_OP = 'UPDATE' THEN
                        IF (NEW."PhotoId", NEW."EligibleAt", NEW."RequestedAt") IS DISTINCT FROM
                           (OLD."PhotoId", OLD."EligibleAt", OLD."RequestedAt") OR NEW."Attempts" < OLD."Attempts" OR
                           (OLD."DeletedAt" IS NOT NULL AND NEW."DeletedAt" IS DISTINCT FROM OLD."DeletedAt") THEN
                            RAISE EXCEPTION 'Inspection cleanup identity and first deletion receipt are immutable'
                                USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionPhotoRemovals_Immutable';
                        END IF;
                        IF OLD."DeletedAt" IS NULL AND NEW."DeletedAt" IS NOT NULL AND
                           (OLD."ClaimId" IS NULL OR OLD."ClaimExpiresAt" <= clock_timestamp() OR
                            NEW."ClaimId" IS DISTINCT FROM OLD."ClaimId" OR NEW."DeletedAt" > clock_timestamp()) THEN
                            RAISE EXCEPTION 'Deletion receipt requires the current unexpired claim'
                                USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionPhotoRemovals_Claim';
                        END IF;
                        RETURN NEW;
                    END IF;
                    UPDATE "InspectionPhotos" SET "AvailableAt" = "AvailableAt" WHERE "Id" = NEW."PhotoId"
                        RETURNING * INTO photo;
                    IF photo."Id" IS NOT NULL AND (NEW."EligibleAt" <> photo."ExpiresAt" OR
                       NEW."EligibleAt" > clock_timestamp() OR NEW."RequestedAt" > clock_timestamp() OR
                       NEW."DeletedAt" IS NOT NULL OR EXISTS
                       (SELECT 1 FROM "InspectionDraftPhotos" WHERE "PhotoId" = NEW."PhotoId")) THEN
                        RAISE EXCEPTION 'Only expired unattached inspection uploads may be cleaned'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionPhotoRemovals_Eligibility';
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER inspection_photo_removal_guard BEFORE INSERT OR UPDATE OR DELETE ON "InspectionPhotoRemovals"
                    FOR EACH ROW EXECUTE FUNCTION guard_inspection_photo_removal();

                CREATE FUNCTION prevent_removed_inspection_photo_availability() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF OLD."AvailableAt" IS NULL AND NEW."AvailableAt" IS NOT NULL AND EXISTS
                        (SELECT 1 FROM "InspectionPhotoRemovals" WHERE "PhotoId" = NEW."Id") THEN
                        RAISE EXCEPTION 'An inspection upload with removal intent cannot become available'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionPhotos_Removal';
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER prevent_removed_inspection_photo_availability BEFORE UPDATE ON "InspectionPhotos"
                    FOR EACH ROW EXECUTE FUNCTION prevent_removed_inspection_photo_availability();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP FUNCTION require_inspection_save_photos() CASCADE;
                DROP FUNCTION guard_inspection_draft_photo() CASCADE;
                DROP FUNCTION guard_inspection_photo_removal() CASCADE;
                DROP FUNCTION prevent_removed_inspection_photo_availability() CASCADE;
                """);
            migrationBuilder.DropTable(
                name: "InspectionDraftPhotos");

            migrationBuilder.DropTable(
                name: "InspectionPhotoRemovals");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_InspectionPhotos_Id_InspectionId_PointCode",
                table: "InspectionPhotos");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_InspectionDraftSaves_Id_InspectionId",
                table: "InspectionDraftSaves");

            migrationBuilder.DropColumn(
                name: "PhotoIdsJson",
                table: "InspectionDraftSaves");
        }
    }
}

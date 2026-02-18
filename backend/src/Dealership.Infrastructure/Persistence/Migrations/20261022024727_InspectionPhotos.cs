using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dealership.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InspectionPhotos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InspectionPhotos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InspectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignmentVersion = table.Column<long>(type: "bigint", nullable: false),
                    PointCode = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    ObjectKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Bytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Width = table.Column<int>(type: "integer", nullable: false),
                    Height = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AvailableAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InspectionPhotos", x => x.Id);
                    table.CheckConstraint("CK_InspectionPhotos_Content", "\"ContentType\" IN ('image/jpeg', 'image/png', 'image/webp') AND \"Bytes\" BETWEEN 1 AND 10485760 AND \"Sha256\" ~ '^[0-9a-f]{64}$' AND \"Width\" > 0 AND \"Height\" > 0");
                    table.CheckConstraint("CK_InspectionPhotos_Identity", "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"AssignmentVersion\" > 0 AND \"ObjectKey\" = 'inspection/photos/' || replace(\"Id\"::text, '-', '')");
                    table.CheckConstraint("CK_InspectionPhotos_Lifetime", "isfinite(\"CreatedAt\") AND isfinite(\"ExpiresAt\") AND \"ExpiresAt\" > \"CreatedAt\" AND (\"AvailableAt\" IS NULL OR (isfinite(\"AvailableAt\") AND \"AvailableAt\" >= \"CreatedAt\" AND \"AvailableAt\" < \"ExpiresAt\"))");
                    table.CheckConstraint("CK_InspectionPhotos_Point", "\"PointCode\" ~ '^(FC|ET|EP|IE|UB)-[0-9]{3}$' AND right(\"PointCode\", 3)::integer BETWEEN 1 AND CASE left(\"PointCode\", 2) WHEN 'FC' THEN 45 WHEN 'ET' THEN 50 WHEN 'EP' THEN 45 WHEN 'IE' THEN 40 WHEN 'UB' THEN 30 END");
                    table.ForeignKey(
                        name: "FK_InspectionPhotos_AspNetUsers_ActorId",
                        column: x => x.ActorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InspectionPhotos_InspectionDrafts_InspectionId_BranchId",
                        columns: x => new { x.InspectionId, x.BranchId },
                        principalTable: "InspectionDrafts",
                        principalColumns: ["Id", "BranchId"],
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InspectionPhotos_ActorId",
                table: "InspectionPhotos",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionPhotos_ExpiresAt",
                table: "InspectionPhotos",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionPhotos_InspectionId_BranchId",
                table: "InspectionPhotos",
                columns: ["InspectionId", "BranchId"]);

            migrationBuilder.CreateIndex(
                name: "IX_InspectionPhotos_InspectionId_PointCode_CreatedAt_Id",
                table: "InspectionPhotos",
                columns: ["InspectionId", "PointCode", "CreatedAt", "Id"]);

            migrationBuilder.Sql("""
                CREATE FUNCTION guard_inspection_photo() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE
                    actor "AspNetUsers"%ROWTYPE;
                    draft "InspectionDrafts"%ROWTYPE;
                    vehicle_id uuid;
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        RAISE EXCEPTION 'Inspection photo metadata is retained'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionPhotos_Immutable';
                    END IF;
                    IF TG_OP = 'UPDATE' THEN
                        IF (to_jsonb(NEW) - 'AvailableAt') IS DISTINCT FROM (to_jsonb(OLD) - 'AvailableAt') OR
                           (OLD."AvailableAt" IS NOT NULL AND NEW."AvailableAt" IS DISTINCT FROM OLD."AvailableAt") THEN
                            RAISE EXCEPTION 'Inspection photo identity, content and first receipt are immutable'
                                USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionPhotos_Immutable';
                        END IF;
                        IF NEW."AvailableAt" IS NOT DISTINCT FROM OLD."AvailableAt" THEN
                            RETURN NEW;
                        END IF;
                    ELSIF NEW."AvailableAt" IS NOT NULL THEN
                        RAISE EXCEPTION 'Inspection uploads must begin pending'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionPhotos_Pending';
                    END IF;

                    SELECT * INTO actor FROM "AspNetUsers" WHERE "Id" = NEW."ActorId" FOR SHARE;
                    PERFORM 1 FROM "Branches" WHERE "Id" = NEW."BranchId" AND "IsActive" FOR SHARE;
                    IF NOT FOUND OR actor."Id" IS NULL OR NOT actor."IsActive" OR
                       actor."Role" <> 'WorkshopTechnician' OR actor."BranchId" IS DISTINCT FROM NEW."BranchId" THEN
                        RAISE EXCEPTION 'Photo requires a current active branch technician'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionPhotos_Eligibility';
                    END IF;
                    SELECT "VehicleId" INTO vehicle_id FROM "InspectionDrafts" WHERE "Id" = NEW."InspectionId";
                    PERFORM 1 FROM "Vehicles" WHERE "Id" = vehicle_id AND "Status" = 'PendingInspection' FOR UPDATE;
                    IF NOT FOUND THEN
                        RAISE EXCEPTION 'Photo requires a pending vehicle'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionPhotos_Vehicle';
                    END IF;
                    SELECT * INTO draft FROM "InspectionDrafts" WHERE "Id" = NEW."InspectionId" FOR UPDATE;
                    IF draft."Id" IS NULL OR draft."BranchId" <> NEW."BranchId" OR
                       draft."TechnicianId" <> NEW."ActorId" OR draft."AssignmentVersion" <> NEW."AssignmentVersion" THEN
                        RAISE EXCEPTION 'Photo requires the current assigned technician and assignment version'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionPhotos_Assignment';
                    END IF;
                    IF NEW."CreatedAt" < draft."CreatedAt" OR NEW."CreatedAt" > clock_timestamp() OR
                       NEW."ExpiresAt" <= clock_timestamp() OR NEW."AvailableAt" > clock_timestamp() THEN
                        RAISE EXCEPTION 'Photo upload must be current and unexpired'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionPhotos_Current';
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER inspection_photo_guard BEFORE INSERT OR UPDATE OR DELETE ON "InspectionPhotos"
                    FOR EACH ROW EXECUTE FUNCTION guard_inspection_photo();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InspectionPhotos");

            migrationBuilder.Sql("DROP FUNCTION guard_inspection_photo();");
        }
    }
}

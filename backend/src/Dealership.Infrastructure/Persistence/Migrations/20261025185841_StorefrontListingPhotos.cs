using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dealership.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StorefrontListingPhotos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid[]>(
                name: "PhotoIds",
                table: "ListingRevisions",
                type: "uuid[]",
                nullable: false,
                defaultValueSql: "'{}'::uuid[]");

            migrationBuilder.CreateTable(
                name: "ListingPhotos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ObjectKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Bytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Width = table.Column<int>(type: "integer", nullable: false),
                    Height = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AvailableAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ListingPhotos", x => x.Id);
                    table.UniqueConstraint("AK_ListingPhotos_Id_VehicleId_BranchId", x => new { x.Id, x.VehicleId, x.BranchId });
                    table.CheckConstraint("CK_ListingPhotos_Shape", "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"ContentType\" IN ('image/jpeg', 'image/png', 'image/webp') AND \"Bytes\" BETWEEN 1 AND 10485760 AND \"Width\" > 0 AND \"Height\" > 0 AND \"Sha256\" ~ '^[a-f0-9]{64}$' AND \"ObjectKey\" = 'listings/photos/' || replace(\"Id\"::text, '-', '') AND isfinite(\"CreatedAt\") AND (\"AvailableAt\" IS NULL OR (isfinite(\"AvailableAt\") AND \"AvailableAt\" >= \"CreatedAt\"))");
                    table.ForeignKey(
                        name: "FK_ListingPhotos_AspNetUsers_ActorId",
                        column: x => x.ActorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ListingPhotos_Vehicles_VehicleId_BranchId",
                        columns: x => new { x.VehicleId, x.BranchId },
                        principalTable: "Vehicles",
                        principalColumns: ["Id", "BranchId"],
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ListingRevisionPhotos",
                columns: table => new
                {
                    RevisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PhotoId = table.Column<Guid>(type: "uuid", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ListingRevisionPhotos", x => new { x.RevisionId, x.PhotoId });
                    table.CheckConstraint("CK_ListingRevisionPhotos_Position", "\"Position\" >= 0");
                    table.ForeignKey(
                        name: "FK_ListingRevisionPhotos_ListingPhotos_PhotoId_VehicleId_Branc~",
                        columns: x => new { x.PhotoId, x.VehicleId, x.BranchId },
                        principalTable: "ListingPhotos",
                        principalColumns: ["Id", "VehicleId", "BranchId"],
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ListingRevisionPhotos_ListingRevisions_RevisionId_VehicleId~",
                        columns: x => new { x.RevisionId, x.VehicleId, x.BranchId },
                        principalTable: "ListingRevisions",
                        principalColumns: ["Id", "VehicleId", "BranchId"],
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ListingPhotos_ActorId",
                table: "ListingPhotos",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_ListingPhotos_VehicleId_BranchId",
                table: "ListingPhotos",
                columns: ["VehicleId", "BranchId"]);

            migrationBuilder.CreateIndex(
                name: "IX_ListingRevisionPhotos_PhotoId_VehicleId_BranchId",
                table: "ListingRevisionPhotos",
                columns: ["PhotoId", "VehicleId", "BranchId"]);

            migrationBuilder.CreateIndex(
                name: "IX_ListingRevisionPhotos_RevisionId_Position",
                table: "ListingRevisionPhotos",
                columns: ["RevisionId", "Position"],
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ListingRevisionPhotos_RevisionId_VehicleId_BranchId",
                table: "ListingRevisionPhotos",
                columns: ["RevisionId", "VehicleId", "BranchId"]);

            migrationBuilder.Sql("""
                CREATE FUNCTION guard_listing_photo() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE actor "AspNetUsers"%ROWTYPE;
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        RAISE EXCEPTION 'Listing photo metadata is retained'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_ListingPhotos_Immutable';
                    END IF;
                    IF TG_OP = 'UPDATE' THEN
                        IF (to_jsonb(NEW) - 'AvailableAt') IS DISTINCT FROM (to_jsonb(OLD) - 'AvailableAt') OR
                           (OLD."AvailableAt" IS NOT NULL AND NEW."AvailableAt" IS DISTINCT FROM OLD."AvailableAt") THEN
                            RAISE EXCEPTION 'Photo identity, content and first receipt are immutable'
                                USING ERRCODE = '23514', CONSTRAINT = 'CK_ListingPhotos_Immutable';
                        END IF;
                        IF NEW."AvailableAt" IS NOT DISTINCT FROM OLD."AvailableAt" THEN
                            RETURN NEW;
                        END IF;
                    ELSIF NEW."AvailableAt" IS NOT NULL THEN
                        RAISE EXCEPTION 'Listing uploads must begin pending'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_ListingPhotos_Pending';
                    END IF;

                    PERFORM pg_advisory_xact_lock(hashtextextended('listing-photo:' || NEW."Id"::text, 0));
                    SELECT * INTO actor FROM "AspNetUsers" WHERE "Id" = NEW."ActorId" FOR SHARE;
                    PERFORM 1 FROM "Branches" WHERE "Id" = NEW."BranchId" AND "IsActive" FOR SHARE;
                    IF NOT FOUND OR actor."Id" IS NULL OR NOT actor."IsActive" OR
                       actor."Role" NOT IN ('BranchManager', 'GeneralManager') OR
                       (actor."Role" = 'BranchManager' AND actor."BranchId" IS DISTINCT FROM NEW."BranchId") THEN
                        RAISE EXCEPTION 'Photo requires a current manager in scope'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_ListingPhotos_Eligibility';
                    END IF;
                    PERFORM 1 FROM "Vehicles" WHERE "Id" = NEW."VehicleId" AND "BranchId" = NEW."BranchId"
                        AND "CreatedAt" <= NEW."CreatedAt" FOR UPDATE;
                    IF NOT FOUND OR NEW."CreatedAt" > clock_timestamp() OR NEW."AvailableAt" > clock_timestamp() THEN
                        RAISE EXCEPTION 'Photo requires a matching vehicle and current timestamps'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_ListingPhotos_Current';
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER listing_photo_guard BEFORE INSERT OR UPDATE OR DELETE ON "ListingPhotos"
                    FOR EACH ROW EXECUTE FUNCTION guard_listing_photo();

                CREATE FUNCTION guard_listing_revision_photo() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE
                    revision "ListingRevisions"%ROWTYPE;
                    photo "ListingPhotos"%ROWTYPE;
                BEGIN
                    IF TG_OP <> 'INSERT' THEN
                        RAISE EXCEPTION 'Saved listing photo references are immutable'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_ListingRevisionPhotos_Immutable';
                    END IF;
                    SELECT * INTO revision FROM "ListingRevisions" WHERE "Id" = NEW."RevisionId" FOR SHARE;
                    SELECT * INTO photo FROM "ListingPhotos" WHERE "Id" = NEW."PhotoId" FOR SHARE;
                    IF revision."PhotoIds"[NEW."Position" + 1] IS DISTINCT FROM NEW."PhotoId" OR
                       photo."Id" IS NULL OR photo."AvailableAt" IS NULL OR photo."AvailableAt" > revision."RecordedAt" THEN
                        RAISE EXCEPTION 'Only declared available photos may be attached in their saved order'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_ListingRevisionPhotos_Available';
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER listing_revision_photo_guard BEFORE INSERT OR UPDATE OR DELETE ON "ListingRevisionPhotos"
                    FOR EACH ROW EXECUTE FUNCTION guard_listing_revision_photo();

                CREATE FUNCTION check_listing_revision_photos() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF NEW."PhotoIds" IS DISTINCT FROM coalesce((SELECT array_agg("PhotoId" ORDER BY "Position")
                        FROM "ListingRevisionPhotos" WHERE "RevisionId" = NEW."Id"), '{}'::uuid[]) THEN
                        RAISE EXCEPTION 'The saved listing must contain exactly its declared ordered photos'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_ListingRevisionPhotos_Complete';
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE CONSTRAINT TRIGGER listing_revision_photos_complete AFTER INSERT ON "ListingRevisions"
                    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION check_listing_revision_photos();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER listing_revision_photos_complete ON "ListingRevisions";
                DROP TRIGGER listing_revision_photo_guard ON "ListingRevisionPhotos";
                DROP TRIGGER listing_photo_guard ON "ListingPhotos";
                DROP FUNCTION check_listing_revision_photos();
                DROP FUNCTION guard_listing_revision_photo();
                DROP FUNCTION guard_listing_photo();
                """);

            migrationBuilder.DropTable(
                name: "ListingRevisionPhotos");

            migrationBuilder.DropTable(
                name: "ListingPhotos");

            migrationBuilder.DropColumn(
                name: "PhotoIds",
                table: "ListingRevisions");
        }
    }
}

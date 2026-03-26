using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dealership.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StorefrontListings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_InspectionSignedRevisions_Id_BranchId",
                table: "InspectionSignedRevisions",
                columns: ["Id", "BranchId"]);

            migrationBuilder.CreateTable(
                name: "ListingRevisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    VehiclePrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RoadTax = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Transmission = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Fuel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ListingRevisions", x => x.Id);
                    table.UniqueConstraint("AK_ListingRevisions_Id_VehicleId_BranchId", x => new { x.Id, x.VehicleId, x.BranchId });
                    table.CheckConstraint("CK_ListingRevisions_Shape", "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"Version\" > 0 AND \"VehiclePrice\" > 0 AND \"RoadTax\" >= 0 AND isfinite(\"RecordedAt\") AND length(btrim(\"Description\")) BETWEEN 1 AND 2000 AND \"Description\" = btrim(\"Description\") AND \"Transmission\" IN ('Automatic', 'Manual') AND \"Fuel\" IN ('Petrol', 'Diesel', 'Hybrid', 'PlugInHybrid', 'Electric')");
                    table.ForeignKey(
                        name: "FK_ListingRevisions_AspNetUsers_ActorId",
                        column: x => x.ActorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ListingRevisions_Vehicles_VehicleId_BranchId",
                        columns: x => new { x.VehicleId, x.BranchId },
                        principalTable: "Vehicles",
                        principalColumns: ["Id", "BranchId"],
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ListingPublications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RevisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    SignedRevisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ListingPublications", x => x.Id);
                    table.UniqueConstraint("AK_ListingPublications_Id_VehicleId_BranchId", x => new { x.Id, x.VehicleId, x.BranchId });
                    table.CheckConstraint("CK_ListingPublications_Shape", "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND isfinite(\"RecordedAt\")");
                    table.ForeignKey(
                        name: "FK_ListingPublications_AspNetUsers_ActorId",
                        column: x => x.ActorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ListingPublications_InspectionSignedRevisions_SignedRevisio~",
                        columns: x => new { x.SignedRevisionId, x.BranchId },
                        principalTable: "InspectionSignedRevisions",
                        principalColumns: ["Id", "BranchId"],
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ListingPublications_ListingRevisions_RevisionId_VehicleId_B~",
                        columns: x => new { x.RevisionId, x.VehicleId, x.BranchId },
                        principalTable: "ListingRevisions",
                        principalColumns: ["Id", "VehicleId", "BranchId"],
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ListingWithdrawals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PublicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ListingWithdrawals", x => x.Id);
                    table.CheckConstraint("CK_ListingWithdrawals_Shape", "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND isfinite(\"RecordedAt\") AND length(btrim(\"Reason\")) BETWEEN 1 AND 1000 AND \"Reason\" = btrim(\"Reason\") AND \"Reason\" !~ '[[:cntrl:]]'");
                    table.ForeignKey(
                        name: "FK_ListingWithdrawals_AspNetUsers_ActorId",
                        column: x => x.ActorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ListingWithdrawals_ListingPublications_PublicationId_Vehicl~",
                        columns: x => new { x.PublicationId, x.VehicleId, x.BranchId },
                        principalTable: "ListingPublications",
                        principalColumns: ["Id", "VehicleId", "BranchId"],
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ListingPublications_ActorId",
                table: "ListingPublications",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_ListingPublications_RevisionId",
                table: "ListingPublications",
                column: "RevisionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ListingPublications_RevisionId_VehicleId_BranchId",
                table: "ListingPublications",
                columns: ["RevisionId", "VehicleId", "BranchId"]);

            migrationBuilder.CreateIndex(
                name: "IX_ListingPublications_SignedRevisionId_BranchId",
                table: "ListingPublications",
                columns: ["SignedRevisionId", "BranchId"]);

            migrationBuilder.CreateIndex(
                name: "IX_ListingRevisions_ActorId",
                table: "ListingRevisions",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_ListingRevisions_VehicleId_BranchId",
                table: "ListingRevisions",
                columns: ["VehicleId", "BranchId"]);

            migrationBuilder.CreateIndex(
                name: "IX_ListingRevisions_VehicleId_Version",
                table: "ListingRevisions",
                columns: ["VehicleId", "Version"],
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ListingWithdrawals_ActorId",
                table: "ListingWithdrawals",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_ListingWithdrawals_PublicationId",
                table: "ListingWithdrawals",
                column: "PublicationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ListingWithdrawals_PublicationId_VehicleId_BranchId",
                table: "ListingWithdrawals",
                columns: ["PublicationId", "VehicleId", "BranchId"]);

            migrationBuilder.Sql("""
                CREATE FUNCTION guard_listing_operation() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE
                    actor "AspNetUsers"%ROWTYPE;
                    vehicle "Vehicles"%ROWTYPE;
                    revision "ListingRevisions"%ROWTYPE;
                    publication "ListingPublications"%ROWTYPE;
                    latest_version bigint;
                    latest_publication uuid;
                BEGIN
                    IF TG_OP <> 'INSERT' THEN
                        RAISE EXCEPTION 'Listing revisions, approvals and withdrawals are immutable'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_Listings_Immutable';
                    END IF;
                    PERFORM pg_advisory_xact_lock(hashtextextended('listing:' || NEW."Id"::text, 0));
                    SELECT * INTO actor FROM "AspNetUsers" WHERE "Id" = NEW."ActorId" FOR SHARE;
                    PERFORM 1 FROM "Branches" WHERE "Id" = NEW."BranchId" AND "IsActive" FOR SHARE;
                    IF NOT FOUND OR actor."Id" IS NULL OR NOT actor."IsActive" OR
                       actor."Role" NOT IN ('BranchManager', 'GeneralManager') OR
                       (actor."Role" = 'BranchManager' AND actor."BranchId" IS DISTINCT FROM NEW."BranchId") THEN
                        RAISE EXCEPTION 'Listing operations require a current manager in scope'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_Listings_Eligibility';
                    END IF;
                    SELECT * INTO vehicle FROM "Vehicles" WHERE "Id" = NEW."VehicleId"
                        AND "BranchId" = NEW."BranchId" FOR UPDATE;
                    IF vehicle."Id" IS NULL OR NEW."RecordedAt" < vehicle."CreatedAt" OR NEW."RecordedAt" > clock_timestamp() THEN
                        RAISE EXCEPTION 'Listing operations require a matching vehicle and current timestamp'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_Listings_Vehicle';
                    END IF;
                    -- Fence older repeatable-read approvals even when inspection status stays unchanged.
                    UPDATE "Vehicles" SET "Status" = "Status" WHERE "Id" = vehicle."Id";
                    IF (TG_TABLE_NAME <> 'ListingRevisions' AND EXISTS (SELECT 1 FROM "ListingRevisions" WHERE "Id" = NEW."Id")) OR
                       (TG_TABLE_NAME <> 'ListingPublications' AND EXISTS (SELECT 1 FROM "ListingPublications" WHERE "Id" = NEW."Id")) OR
                       (TG_TABLE_NAME <> 'ListingWithdrawals' AND EXISTS (SELECT 1 FROM "ListingWithdrawals" WHERE "Id" = NEW."Id")) THEN
                        RAISE EXCEPTION 'An operation identity cannot be reused for a different listing command'
                            USING ERRCODE = '23505', CONSTRAINT = 'UX_Listings_Operation';
                    END IF;
                    SELECT coalesce(max("Version"), 0) INTO latest_version FROM "ListingRevisions" WHERE "VehicleId" = NEW."VehicleId";
                    IF TG_TABLE_NAME = 'ListingRevisions' THEN
                        IF latest_version = 9223372036854775807 OR NEW."Version" <> latest_version + 1 OR EXISTS (
                            SELECT 1 FROM "ListingRevisions" WHERE "VehicleId" = NEW."VehicleId" AND "RecordedAt" > NEW."RecordedAt") THEN
                            RAISE EXCEPTION 'Listing edits require the next current revision'
                                USING ERRCODE = '23514', CONSTRAINT = 'CK_ListingRevisions_Version';
                        END IF;
                        RETURN NEW;
                    END IF;
                    IF TG_TABLE_NAME = 'ListingPublications' THEN
                        SELECT * INTO revision FROM "ListingRevisions" WHERE "Id" = NEW."RevisionId";
                        IF revision."Id" IS NULL OR revision."VehicleId" <> NEW."VehicleId" OR
                           revision."BranchId" <> NEW."BranchId" OR revision."Version" <> latest_version OR
                           NEW."RecordedAt" < revision."RecordedAt" OR vehicle."Status" <> 'Available' OR NOT EXISTS (
                            SELECT 1 FROM "InspectionDrafts" draft JOIN "InspectionSignedRevisions" signed
                                ON signed."InspectionId" = draft."Id" AND signed."Revision" = draft."Revision"
                            WHERE draft."VehicleId" = NEW."VehicleId" AND draft."IsSigned" AND
                                signed."Id" = NEW."SignedRevisionId" AND signed."BranchId" = NEW."BranchId" AND
                                signed."SignedAt" <= NEW."RecordedAt") THEN
                            RAISE EXCEPTION 'Publication requires the current listing and eligible signed inspection'
                                USING ERRCODE = '23514', CONSTRAINT = 'CK_ListingPublications_Current';
                        END IF;
                        RETURN NEW;
                    END IF;
                    SELECT * INTO publication FROM "ListingPublications" WHERE "Id" = NEW."PublicationId";
                    SELECT p."Id" INTO latest_publication FROM "ListingPublications" p
                        JOIN "ListingRevisions" r ON r."Id" = p."RevisionId"
                        WHERE p."VehicleId" = NEW."VehicleId" ORDER BY r."Version" DESC LIMIT 1;
                    IF publication."Id" IS NULL OR publication."VehicleId" <> NEW."VehicleId" OR
                       publication."BranchId" <> NEW."BranchId" OR publication."Id" IS DISTINCT FROM latest_publication OR
                       NEW."RecordedAt" < publication."RecordedAt" THEN
                        RAISE EXCEPTION 'Withdrawal requires the latest approved publication'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_ListingWithdrawals_Current';
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER listing_revision_guard BEFORE INSERT OR UPDATE OR DELETE ON "ListingRevisions"
                    FOR EACH ROW EXECUTE FUNCTION guard_listing_operation();
                CREATE TRIGGER listing_publication_guard BEFORE INSERT OR UPDATE OR DELETE ON "ListingPublications"
                    FOR EACH ROW EXECUTE FUNCTION guard_listing_operation();
                CREATE TRIGGER listing_withdrawal_guard BEFORE INSERT OR UPDATE OR DELETE ON "ListingWithdrawals"
                    FOR EACH ROW EXECUTE FUNCTION guard_listing_operation();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER listing_revision_guard ON "ListingRevisions";
                DROP TRIGGER listing_publication_guard ON "ListingPublications";
                DROP TRIGGER listing_withdrawal_guard ON "ListingWithdrawals";
                DROP FUNCTION guard_listing_operation();
                """);

            migrationBuilder.DropTable(
                name: "ListingWithdrawals");

            migrationBuilder.DropTable(
                name: "ListingPublications");

            migrationBuilder.DropTable(
                name: "ListingRevisions");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_InspectionSignedRevisions_Id_BranchId",
                table: "InspectionSignedRevisions");
        }
    }
}

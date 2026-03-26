using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dealership.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StorefrontReservations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_ListingWithdrawals_VehicleId_RecordedAt",
                table: "ListingWithdrawals",
                columns: ["VehicleId", "RecordedAt"]);

            migrationBuilder.CreateTable(
                name: "Reservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    PublicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PromoId = table.Column<Guid>(type: "uuid", nullable: true),
                    PromoCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true, collation: "C"),
                    SstRate = table.Column<decimal>(type: "numeric(29,28)", precision: 29, scale: 28, nullable: false),
                    Deposit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    BuyerEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    BuyerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BuyerMobile = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    BuyerNric = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    ProcessingFee = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PromoDiscount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RoadTax = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ServiceTax = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Total = table.Column<decimal>(type: "numeric(20,2)", precision: 20, scale: 2, nullable: false),
                    VehiclePrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reservations", x => x.Id);
                    table.CheckConstraint("CK_Reservations_Buyer", "\"BuyerName\" ~ '[^[:space:]]' AND \"BuyerEmail\" ~ '[^[:space:]]' AND \"BuyerMobile\" ~ '[^[:space:]]' AND \"BuyerNric\" ~ '^[0-9]{12}$'");
                    table.CheckConstraint("CK_Reservations_Price", "\"VehiclePrice\" > 0 AND \"VehiclePrice\" <> 'NaN'::numeric AND \"ProcessingFee\" >= 0 AND \"ProcessingFee\" <> 'NaN'::numeric AND \"RoadTax\" >= 0 AND \"RoadTax\" <> 'NaN'::numeric AND \"SstRate\" BETWEEN 0 AND 1 AND \"ServiceTax\" = round(\"ProcessingFee\" * \"SstRate\", 2) AND \"PromoDiscount\" BETWEEN 0 AND \"VehiclePrice\" AND \"Total\" = \"VehiclePrice\" + \"ProcessingFee\" + \"RoadTax\" + \"ServiceTax\" - \"PromoDiscount\"");
                    table.CheckConstraint("CK_Reservations_Promo", "(\"PromoId\" IS NULL AND \"PromoCode\" IS NULL AND \"PromoDiscount\" = 0) OR (\"PromoId\" IS NOT NULL AND \"PromoCode\" IS NOT NULL AND length(\"PromoCode\") BETWEEN 1 AND 50)");
                    table.CheckConstraint("CK_Reservations_Shape", "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND isfinite(\"CreatedAt\") AND isfinite(\"ExpiresAt\") AND \"ExpiresAt\" = \"CreatedAt\" + interval '48 hours' AND \"Deposit\" = 500");
                    table.ForeignKey(
                        name: "FK_Reservations_CustomerProfiles_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "CustomerProfiles",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Reservations_ListingPublications_PublicationId_VehicleId_Br~",
                        columns: x => new { x.PublicationId, x.VehicleId, x.BranchId },
                        principalTable: "ListingPublications",
                        principalColumns: ["Id", "VehicleId", "BranchId"],
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Reservations_PromoCodes_PromoId",
                        column: x => x.PromoId,
                        principalTable: "PromoCodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReservationReleases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Refund = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReservationReleases", x => x.Id);
                    table.CheckConstraint("CK_ReservationReleases_Actor", "(\"Kind\" = 'CustomerCancelled' AND \"ActorId\" IS NOT NULL AND \"Reason\" IS NULL) OR (\"Kind\" = 'StaffCancelled' AND \"ActorId\" IS NOT NULL AND \"Reason\" IS NOT NULL AND length(btrim(\"Reason\")) BETWEEN 1 AND 1000 AND \"Reason\" = btrim(\"Reason\") AND \"Reason\" !~ '[[:cntrl:]]') OR (\"Kind\" IN ('Expired', 'Withdrawn', 'Reinspection') AND \"ActorId\" IS NULL AND \"Reason\" IS NULL)");
                    table.CheckConstraint("CK_ReservationReleases_Shape", "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND isfinite(\"RecordedAt\") AND \"Refund\" = 500 AND \"Kind\" IN ('CustomerCancelled', 'StaffCancelled', 'Expired', 'Withdrawn', 'Reinspection')");
                    table.ForeignKey(
                        name: "FK_ReservationReleases_AspNetUsers_ActorId",
                        column: x => x.ActorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReservationReleases_Reservations_ReservationId",
                        column: x => x.ReservationId,
                        principalTable: "Reservations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReservationReleases_ActorId",
                table: "ReservationReleases",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_ReservationReleases_ReservationId",
                table: "ReservationReleases",
                column: "ReservationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_BranchId_CreatedAt_Id",
                table: "Reservations",
                columns: ["BranchId", "CreatedAt", "Id"]);

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_CustomerId",
                table: "Reservations",
                column: "CustomerId",
                unique: true,
                filter: "\"IsActive\"");

            migrationBuilder.Sql("""
                CREATE FUNCTION guard_reservation() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE
                    customer "AspNetUsers"%ROWTYPE;
                    profile "CustomerProfiles"%ROWTYPE;
                    vehicle "Vehicles"%ROWTYPE;
                    publication "ListingPublications"%ROWTYPE;
                    revision "ListingRevisions"%ROWTYPE;
                    settings "PurchaseSettings"%ROWTYPE;
                    promo "PromoCodes"%ROWTYPE;
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        RAISE EXCEPTION 'Reservation confirmations are retained'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_Reservations_Immutable';
                    END IF;
                    IF TG_OP = 'UPDATE' THEN
                        IF OLD."IsActive" AND NOT NEW."IsActive" AND
                           (to_jsonb(OLD) - 'IsActive') = (to_jsonb(NEW) - 'IsActive') AND
                           EXISTS (SELECT 1 FROM "ReservationReleases" WHERE "ReservationId" = OLD."Id") THEN
                            RETURN NEW;
                        END IF;
                        RAISE EXCEPTION 'Only a retained release can free an active reservation'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_Reservations_Immutable';
                    END IF;
                    PERFORM pg_advisory_xact_lock(hashtextextended('reservation:' || NEW."Id"::text, 0));
                    SELECT * INTO customer FROM "AspNetUsers" WHERE "Id" = NEW."CustomerId" FOR UPDATE;
                    SELECT * INTO profile FROM "CustomerProfiles" WHERE "UserId" = NEW."CustomerId" FOR SHARE;
                    IF customer."Id" IS NULL OR NOT customer."IsActive" OR customer."Role" <> 'Customer' OR
                       NOT customer."EmailConfirmed" OR profile."UserId" IS NULL OR
                       ROW(NEW."BuyerName", NEW."BuyerEmail", NEW."BuyerMobile", NEW."BuyerNric") IS DISTINCT FROM
                       ROW(profile."LegalName", customer."Email", profile."Mobile", profile."Nric") THEN
                        RAISE EXCEPTION 'Confirmation requires a current verified customer and exact account snapshot'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_Reservations_Customer';
                    END IF;
                    PERFORM 1 FROM "Branches" WHERE "Id" = NEW."BranchId" AND "IsActive" FOR SHARE;
                    IF NOT FOUND THEN
                        RAISE EXCEPTION 'Reservation branch is unavailable'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_Reservations_Eligibility';
                    END IF;
                    SELECT * INTO vehicle FROM "Vehicles" WHERE "Id" = NEW."VehicleId" AND "BranchId" = NEW."BranchId" FOR UPDATE;
                    SELECT * INTO publication FROM "ListingPublications" WHERE "Id" = NEW."PublicationId";
                    SELECT * INTO revision FROM "ListingRevisions" WHERE "Id" = publication."RevisionId";
                    IF vehicle."Id" IS NULL OR vehicle."Status" <> 'Available' OR NOT NEW."IsActive" OR
                       publication."Id" IS NULL OR publication."VehicleId" <> NEW."VehicleId" OR
                       publication."BranchId" <> NEW."BranchId" OR NEW."CreatedAt" < publication."RecordedAt" OR
                       NEW."CreatedAt" > clock_timestamp() OR NEW."ExpiresAt" <= clock_timestamp() OR
                       EXISTS (SELECT 1 FROM "ListingWithdrawals" WHERE "PublicationId" = publication."Id") OR
                       EXISTS (SELECT 1 FROM "ListingPublications" p JOIN "ListingRevisions" r ON r."Id" = p."RevisionId"
                           WHERE p."VehicleId" = NEW."VehicleId" AND r."Version" > revision."Version") OR
                       NOT EXISTS (SELECT 1 FROM "InspectionSignedRevisions" s JOIN "InspectionDrafts" d ON d."Id" = s."InspectionId"
                           WHERE s."Id" = publication."SignedRevisionId" AND d."VehicleId" = NEW."VehicleId" AND
                               d."IsSigned" AND d."Revision" = s."Revision") THEN
                        RAISE EXCEPTION 'Confirmation requires the current eligible approved publication'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_Reservations_Eligibility';
                    END IF;
                    SELECT * INTO settings FROM "PurchaseSettings" WHERE "Id" = 1 FOR SHARE;
                    SELECT * INTO promo FROM "PromoCodes" WHERE "Id" = NEW."PromoId" FOR SHARE;
                    IF settings."Id" IS NULL OR
                       ROW(NEW."VehiclePrice", NEW."RoadTax", NEW."ProcessingFee", NEW."SstRate") IS DISTINCT FROM
                       ROW(revision."VehiclePrice", revision."RoadTax", settings."ProcessingFee", settings."SstRate") OR
                       (NEW."PromoId" IS NOT NULL AND (promo."Id" IS NULL OR NOT promo."IsActive" OR
                           promo."ExpiresAtUtc" <= clock_timestamp() OR promo."Discount" > revision."VehiclePrice" OR
                           ROW(NEW."PromoCode", NEW."PromoDiscount") IS DISTINCT FROM ROW(promo."Code", promo."Discount"))) THEN
                        RAISE EXCEPTION 'Confirmation must retain current canonical pricing and accepted promo'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_Reservations_Terms';
                    END IF;
                    IF EXISTS (SELECT 1 FROM "ReservationReleases" WHERE "Id" = NEW."Id") THEN
                        RAISE EXCEPTION 'Reservation operation identity already belongs to a release'
                            USING ERRCODE = '23505', CONSTRAINT = 'UX_Reservations_Operation';
                    END IF;
                    UPDATE "Vehicles" SET "Status" = "Status" WHERE "Id" = vehicle."Id";
                    RETURN NEW;
                END $$;
                CREATE TRIGGER reservation_guard BEFORE INSERT OR UPDATE OR DELETE ON "Reservations"
                    FOR EACH ROW EXECUTE FUNCTION guard_reservation();

                CREATE FUNCTION guard_reservation_release() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE
                    held "Reservations"%ROWTYPE;
                    actor "AspNetUsers"%ROWTYPE;
                BEGIN
                    IF TG_OP <> 'INSERT' THEN
                        RAISE EXCEPTION 'Release and full simulated refund receipts are immutable'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_ReservationReleases_Immutable';
                    END IF;
                    PERFORM pg_advisory_xact_lock(hashtextextended('reservation:' || NEW."Id"::text, 0));
                    SELECT * INTO actor FROM "AspNetUsers" WHERE "Id" = NEW."ActorId" FOR SHARE;
                    SELECT * INTO held FROM "Reservations" WHERE "Id" = NEW."ReservationId";
                    IF held."Id" IS NULL THEN
                        RAISE EXCEPTION 'Release requires a retained reservation'
                            USING ERRCODE = '23503', CONSTRAINT = 'FK_ReservationReleases_Reservations_ReservationId';
                    END IF;
                    IF NEW."Kind" = 'StaffCancelled' AND actor."Role" = 'BranchManager' THEN
                        PERFORM 1 FROM "Branches" WHERE "Id" = held."BranchId" AND "IsActive" FOR SHARE;
                        IF NOT FOUND THEN
                            RAISE EXCEPTION 'Branch manager cancellation requires an active branch'
                                USING ERRCODE = '23514', CONSTRAINT = 'CK_ReservationReleases_Actor';
                        END IF;
                    END IF;
                    PERFORM 1 FROM "Vehicles" WHERE "Id" = held."VehicleId" FOR UPDATE;
                    IF EXISTS (SELECT 1 FROM "ReservationReleases" WHERE "ReservationId" = held."Id") OR
                       EXISTS (SELECT 1 FROM "Reservations" WHERE "Id" = NEW."Id") THEN
                        RAISE EXCEPTION 'A reservation has only one release and full refund'
                            USING ERRCODE = '23505', CONSTRAINT = 'UX_ReservationReleases_Receipt';
                    END IF;
                    IF NEW."RecordedAt" < held."CreatedAt" OR NEW."RecordedAt" > clock_timestamp() OR
                       (NEW."Kind" = 'Expired') IS DISTINCT FROM (NEW."RecordedAt" >= held."ExpiresAt") OR
                       (NEW."Kind" <> 'Expired' AND clock_timestamp() >= held."ExpiresAt") THEN
                        RAISE EXCEPTION 'Release must observe the authoritative reservation deadline'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_ReservationReleases_Deadline';
                    END IF;
                    IF NEW."Kind" IN ('CustomerCancelled', 'StaffCancelled') AND
                       (actor."Id" IS NULL OR NOT actor."IsActive" OR
                           (NEW."Kind" = 'CustomerCancelled' AND (actor."Role" <> 'Customer' OR actor."Id" <> held."CustomerId")) OR
                           (NEW."Kind" = 'StaffCancelled' AND (actor."Role" NOT IN ('BranchManager', 'GeneralManager') OR
                               (actor."Role" = 'BranchManager' AND actor."BranchId" IS DISTINCT FROM held."BranchId")))) THEN
                        RAISE EXCEPTION 'Cancellation requires the current customer or scoped manager'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_ReservationReleases_Actor';
                    END IF;
                    IF NEW."Kind" = 'Withdrawn' AND NOT EXISTS (SELECT 1 FROM "ListingWithdrawals"
                        WHERE "VehicleId" = held."VehicleId" AND "RecordedAt" >= held."CreatedAt" AND "RecordedAt" <= NEW."RecordedAt") THEN
                        RAISE EXCEPTION 'Withdrawal release requires its retained listing withdrawal'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_ReservationReleases_Cause';
                    END IF;
                    IF NEW."Kind" = 'Reinspection' AND EXISTS (SELECT 1 FROM "ListingPublications" p
                        JOIN "InspectionSignedRevisions" s ON s."Id" = p."SignedRevisionId"
                        JOIN "InspectionDrafts" d ON d."Id" = s."InspectionId"
                        WHERE p."Id" = held."PublicationId" AND d."IsSigned" AND d."Revision" = s."Revision" AND
                            inspection_vehicle_status(held."VehicleId") = 'Available') THEN
                        RAISE EXCEPTION 'Reinspection release requires lost inspection eligibility'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_ReservationReleases_Cause';
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER reservation_release_guard BEFORE INSERT OR UPDATE OR DELETE ON "ReservationReleases"
                    FOR EACH ROW EXECUTE FUNCTION guard_reservation_release();

                CREATE FUNCTION apply_reservation_release() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    UPDATE "Reservations" SET "IsActive" = false WHERE "Id" = NEW."ReservationId";
                    RETURN NULL;
                END $$;
                CREATE TRIGGER reservation_released AFTER INSERT ON "ReservationReleases"
                    FOR EACH ROW EXECUTE FUNCTION apply_reservation_release();

                CREATE FUNCTION require_reservation_release() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "Reservations" r WHERE r."Id" = NEW."Id" AND
                        r."IsActive" = EXISTS (SELECT 1 FROM "ReservationReleases" WHERE "ReservationId" = r."Id")) THEN
                        RAISE EXCEPTION 'Active reservation slots must agree with retained release receipts'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_Reservations_Release';
                    END IF;
                    RETURN NULL;
                END $$;
                CREATE CONSTRAINT TRIGGER reservation_release_consistency AFTER INSERT OR UPDATE ON "Reservations"
                    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION require_reservation_release();

                CREATE FUNCTION release_unavailable_reservation(vehicle_id uuid) RETURNS void LANGUAGE plpgsql AS $$
                DECLARE
                    held "Reservations"%ROWTYPE;
                    release_kind text;
                    recorded_at timestamptz;
                BEGIN
                    PERFORM 1 FROM "Vehicles" WHERE "Id" = vehicle_id FOR UPDATE;
                    SELECT * INTO held FROM "Reservations" WHERE "VehicleId" = vehicle_id AND "IsActive";
                    IF held."Id" IS NULL THEN
                        RETURN;
                    END IF;
                    recorded_at := clock_timestamp();
                    IF recorded_at >= held."ExpiresAt" THEN
                        release_kind := 'Expired';
                    ELSIF EXISTS (SELECT 1 FROM "ListingWithdrawals" WHERE "VehicleId" = vehicle_id AND "RecordedAt" >= held."CreatedAt") THEN
                        release_kind := 'Withdrawn';
                    ELSIF NOT EXISTS (SELECT 1 FROM "ListingPublications" p
                        JOIN "InspectionSignedRevisions" s ON s."Id" = p."SignedRevisionId"
                        JOIN "InspectionDrafts" d ON d."Id" = s."InspectionId"
                        WHERE p."Id" = held."PublicationId" AND d."IsSigned" AND d."Revision" = s."Revision" AND
                            inspection_vehicle_status(held."VehicleId") = 'Available') THEN
                        release_kind := 'Reinspection';
                    ELSE
                        RETURN;
                    END IF;
                    INSERT INTO "ReservationReleases" ("Id", "ReservationId", "Kind", "RecordedAt", "Refund")
                        VALUES (gen_random_uuid(), held."Id", release_kind, recorded_at, held."Deposit");
                END $$;

                CREATE FUNCTION release_changed_vehicle_reservation() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_TABLE_NAME = 'Vehicles' THEN
                        PERFORM release_unavailable_reservation(NEW."Id");
                    ELSE
                        PERFORM release_unavailable_reservation(NEW."VehicleId");
                    END IF;
                    RETURN NULL;
                END $$;
                CREATE TRIGGER listing_withdrawn_reservation AFTER INSERT ON "ListingWithdrawals"
                    FOR EACH ROW EXECUTE FUNCTION release_changed_vehicle_reservation();
                CREATE TRIGGER inspection_changed_reservation AFTER UPDATE OF "IsSigned", "Revision" ON "InspectionDrafts"
                    FOR EACH ROW EXECUTE FUNCTION release_changed_vehicle_reservation();
                CREATE TRIGGER inspection_status_reservation AFTER UPDATE OF "Status" ON "Vehicles"
                    FOR EACH ROW WHEN (NEW."Status" <> 'Available') EXECUTE FUNCTION release_changed_vehicle_reservation();
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_CustomerId_CreatedAt_Id",
                table: "Reservations",
                columns: ["CustomerId", "CreatedAt", "Id"]);

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_ExpiresAt",
                table: "Reservations",
                column: "ExpiresAt",
                filter: "\"IsActive\"");

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_PromoId",
                table: "Reservations",
                column: "PromoId");

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_PublicationId_VehicleId_BranchId",
                table: "Reservations",
                columns: ["PublicationId", "VehicleId", "BranchId"]);

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_VehicleId",
                table: "Reservations",
                column: "VehicleId",
                unique: true,
                filter: "\"IsActive\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER inspection_status_reservation ON "Vehicles";
                DROP TRIGGER inspection_changed_reservation ON "InspectionDrafts";
                DROP TRIGGER listing_withdrawn_reservation ON "ListingWithdrawals";
                DROP FUNCTION release_changed_vehicle_reservation();
                DROP FUNCTION release_unavailable_reservation(uuid);
                DROP TRIGGER reservation_release_consistency ON "Reservations";
                DROP FUNCTION require_reservation_release();
                DROP TRIGGER reservation_released ON "ReservationReleases";
                DROP FUNCTION apply_reservation_release();
                DROP TRIGGER reservation_release_guard ON "ReservationReleases";
                DROP FUNCTION guard_reservation_release();
                DROP TRIGGER reservation_guard ON "Reservations";
                DROP FUNCTION guard_reservation();
                """);

            migrationBuilder.DropIndex(
                name: "IX_ListingWithdrawals_VehicleId_RecordedAt",
                table: "ListingWithdrawals");

            migrationBuilder.DropTable(
                name: "ReservationReleases");

            migrationBuilder.DropTable(
                name: "Reservations");
        }
    }
}

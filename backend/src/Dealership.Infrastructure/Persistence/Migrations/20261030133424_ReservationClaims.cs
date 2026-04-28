using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dealership.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReservationClaims : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_SalesOrderEvents_Shape",
                table: "SalesOrderEvents");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Reservations_Shape",
                table: "Reservations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ReservationReleases_Actor",
                table: "ReservationReleases");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ReservationReleases_Shape",
                table: "ReservationReleases");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ClaimedAt",
                table: "Reservations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ClaimedById",
                table: "Reservations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "FrozenRemaining",
                table: "Reservations",
                type: "interval",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ReservationEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentId = table.Column<Guid>(type: "uuid", nullable: true),
                    PreviousAgentId = table.Column<Guid>(type: "uuid", nullable: true),
                    RecordedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReservationEvents", x => x.Id);
                    table.CheckConstraint("CK_ReservationEvents_Shape", "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND isfinite(\"RecordedAtUtc\") AND \"Sequence\" > 0 AND \"Kind\" IN ('Claimed', 'Assigned', 'Reassigned', 'Released') AND ((\"Kind\" = 'Claimed' AND \"AgentId\" = \"ActorId\" AND \"PreviousAgentId\" IS NULL) OR (\"Kind\" = 'Assigned' AND \"AgentId\" IS NOT NULL AND \"PreviousAgentId\" IS NULL) OR (\"Kind\" = 'Reassigned' AND \"AgentId\" IS NOT NULL AND \"PreviousAgentId\" IS NOT NULL AND \"AgentId\" <> \"PreviousAgentId\") OR (\"Kind\" = 'Released' AND \"AgentId\" IS NULL AND \"PreviousAgentId\" IS NOT NULL))");
                    table.ForeignKey(
                        name: "FK_ReservationEvents_AspNetUsers_ActorId",
                        column: x => x.ActorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReservationEvents_AspNetUsers_AgentId",
                        column: x => x.AgentId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReservationEvents_AspNetUsers_PreviousAgentId",
                        column: x => x.PreviousAgentId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReservationEvents_Reservations_ReservationId",
                        column: x => x.ReservationId,
                        principalTable: "Reservations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_SalesOrderEvents_Shape",
                table: "SalesOrderEvents",
                sql: "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND isfinite(\"RecordedAtUtc\") AND \"Sequence\" > 0 AND \"Kind\" IN ('Created', 'Revised', 'Submitted', 'Approved', 'Rejected', 'Cancelled', 'Reassigned') AND \"ToStatus\" IN ('Draft', 'PendingManagerApproval', 'Approved', 'Cancelled') AND (\"FromStatus\" IS NULL OR \"FromStatus\" IN ('Draft', 'PendingManagerApproval', 'Approved', 'Cancelled')) AND ((\"Kind\" IN ('Rejected', 'Cancelled') AND \"Reason\" IS NOT NULL AND \"Reason\" = btrim(\"Reason\") AND length(\"Reason\") BETWEEN 1 AND 1000 AND \"Reason\" !~ '[[:cntrl:]]') OR (\"Kind\" NOT IN ('Rejected', 'Cancelled') AND \"Reason\" IS NULL))");

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_ClaimedById_ClaimedAt",
                table: "Reservations",
                columns: new[] { "ClaimedById", "ClaimedAt" },
                filter: "\"ClaimedById\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Reservations_Claim",
                table: "Reservations",
                sql: "(\"ClaimedById\" IS NULL AND \"ClaimedAt\" IS NULL AND \"FrozenRemaining\" IS NULL) OR (\"ClaimedById\" IS NOT NULL AND \"ClaimedAt\" IS NOT NULL AND \"ClaimedAt\" >= \"CreatedAt\" AND \"FrozenRemaining\" > interval '0' AND \"FrozenRemaining\" <= interval '48 hours')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Reservations_Shape",
                table: "Reservations",
                sql: "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND isfinite(\"CreatedAt\") AND isfinite(\"ExpiresAt\") AND \"ExpiresAt\" >= \"CreatedAt\" + interval '48 hours' AND \"Deposit\" = 500");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ReservationReleases_Actor",
                table: "ReservationReleases",
                sql: "(\"Kind\" = 'CustomerCancelled' AND \"ActorId\" IS NOT NULL AND \"Reason\" IS NULL) OR (\"Kind\" IN ('StaffCancelled', 'DealCancelled') AND \"ActorId\" IS NOT NULL AND \"Reason\" IS NOT NULL AND length(btrim(\"Reason\")) BETWEEN 1 AND 1000 AND \"Reason\" = btrim(\"Reason\") AND \"Reason\" !~ '[[:cntrl:]]') OR (\"Kind\" IN ('Expired', 'Withdrawn', 'Reinspection') AND \"ActorId\" IS NULL AND \"Reason\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ReservationReleases_Shape",
                table: "ReservationReleases",
                sql: "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND isfinite(\"RecordedAt\") AND \"Refund\" = 500 AND \"Kind\" IN ('CustomerCancelled', 'StaffCancelled', 'Expired', 'Withdrawn', 'Reinspection', 'DealCancelled')");

            migrationBuilder.CreateIndex(
                name: "IX_ReservationEvents_ActorId",
                table: "ReservationEvents",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_ReservationEvents_AgentId",
                table: "ReservationEvents",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_ReservationEvents_PreviousAgentId",
                table: "ReservationEvents",
                column: "PreviousAgentId");

            migrationBuilder.CreateIndex(
                name: "IX_ReservationEvents_ReservationId_Sequence",
                table: "ReservationEvents",
                columns: new[] { "ReservationId", "Sequence" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Reservations_AspNetUsers_ClaimedById",
                table: "Reservations",
                column: "ClaimedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // A claim freezes the hold's timer, so the guards that judged expiry and mutation now know about it.
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION guard_reservation() RETURNS trigger LANGUAGE plpgsql AS $$
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
                        -- A claim, reassignment or release changes only the claim facts and the deadline they freeze.
                        IF OLD."IsActive" AND NEW."IsActive" AND
                           (to_jsonb(OLD) - 'ClaimedById' - 'ClaimedAt' - 'FrozenRemaining' - 'ExpiresAt') =
                           (to_jsonb(NEW) - 'ClaimedById' - 'ClaimedAt' - 'FrozenRemaining' - 'ExpiresAt') THEN
                            PERFORM pg_advisory_xact_lock(hashtextextended('reservation:' || NEW."Id"::text, 0));
                            PERFORM 1 FROM "Vehicles" WHERE "Id" = NEW."VehicleId" FOR UPDATE;
                            IF NEW."ClaimedById" IS NOT NULL THEN
                                PERFORM 1 FROM "AspNetUsers" WHERE "Id" = NEW."ClaimedById" AND "IsActive" AND
                                    "Role" = 'SalesAgent' AND "BranchId" = NEW."BranchId" FOR SHARE;
                                IF NOT FOUND THEN
                                    RAISE EXCEPTION 'A hold can only be handled by an active sales agent of its branch'
                                        USING ERRCODE = '23514', CONSTRAINT = 'CK_Reservations_Claim';
                                END IF;
                                PERFORM 1 FROM "Branches" WHERE "Id" = NEW."BranchId" AND "IsActive" FOR SHARE;
                                IF NOT FOUND THEN
                                    RAISE EXCEPTION 'A hold can only be handled in an active branch'
                                        USING ERRCODE = '23514', CONSTRAINT = 'CK_Reservations_Claim';
                                END IF;
                            END IF;
                            IF OLD."ClaimedById" IS NULL AND NEW."ClaimedById" IS NOT NULL AND
                               NEW."ExpiresAt" = OLD."ExpiresAt" AND NEW."ClaimedAt" < OLD."ExpiresAt" AND
                               NEW."ClaimedAt" <= clock_timestamp() AND
                               NEW."FrozenRemaining" = OLD."ExpiresAt" - NEW."ClaimedAt" THEN
                                RETURN NEW;
                            END IF;
                            IF OLD."ClaimedById" IS NOT NULL AND NEW."ClaimedById" IS NOT NULL AND
                               NEW."ClaimedById" <> OLD."ClaimedById" AND NEW."ExpiresAt" = OLD."ExpiresAt" AND
                               NEW."FrozenRemaining" = OLD."FrozenRemaining" AND NEW."ClaimedAt" >= OLD."ClaimedAt" AND
                               NEW."ClaimedAt" <= clock_timestamp() THEN
                                RETURN NEW;
                            END IF;
                            IF OLD."ClaimedById" IS NOT NULL AND NEW."ClaimedById" IS NULL AND
                               NEW."ExpiresAt" - OLD."FrozenRemaining" >= OLD."ClaimedAt" AND
                               NEW."ExpiresAt" - OLD."FrozenRemaining" <= clock_timestamp() THEN
                                RETURN NEW;
                            END IF;
                        END IF;
                        RAISE EXCEPTION 'Only a retained release or a valid claim change can alter an active reservation'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_Reservations_Immutable';
                    END IF;
                    IF NEW."ClaimedById" IS NOT NULL THEN
                        RAISE EXCEPTION 'A new reservation starts open and unclaimed'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_Reservations_Claim';
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

                CREATE OR REPLACE FUNCTION guard_reservation_release() RETURNS trigger LANGUAGE plpgsql AS $$
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
                    IF NEW."Kind" IN ('StaffCancelled', 'DealCancelled') AND actor."Role" = 'BranchManager' THEN
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
                       (NEW."Kind" = 'Expired') IS DISTINCT FROM (held."ClaimedById" IS NULL AND NEW."RecordedAt" >= held."ExpiresAt") OR
                       (NEW."Kind" <> 'Expired' AND held."ClaimedById" IS NULL AND clock_timestamp() >= held."ExpiresAt") THEN
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
                    IF NEW."Kind" = 'DealCancelled' AND (actor."Id" IS NULL OR NOT actor."IsActive" OR
                        NOT EXISTS (SELECT 1 FROM "SalesOrders" so WHERE so."ReservationId" = held."Id" AND
                            so."Status" = 'Cancelled' AND
                            ((actor."Role" = 'SalesAgent' AND so."SalesAgentId" = actor."Id") OR
                             (actor."Role" = 'BranchManager' AND actor."BranchId" IS NOT DISTINCT FROM held."BranchId") OR
                             actor."Role" = 'GeneralManager'))) THEN
                        RAISE EXCEPTION 'Deal cancellation requires the deal''s agent or a scoped manager and a cancelled deal'
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

                CREATE OR REPLACE FUNCTION release_unavailable_reservation(vehicle_id uuid) RETURNS void LANGUAGE plpgsql AS $$
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
                    IF held."ClaimedById" IS NULL AND recorded_at >= held."ExpiresAt" THEN
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

                CREATE FUNCTION forbid_reservation_event_change() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'Reservation events are append-only.' USING ERRCODE = 'restrict_violation';
                END $$;
                CREATE TRIGGER reservation_events_append_only BEFORE UPDATE OR DELETE ON "ReservationEvents"
                    FOR EACH ROW EXECUTE FUNCTION forbid_reservation_event_change();

                CREATE FUNCTION guard_reservation_event() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE
                    held "Reservations"%ROWTYPE;
                    actor "AspNetUsers"%ROWTYPE;
                    previous "ReservationEvents"%ROWTYPE;
                    expected_agent uuid;
                BEGIN
                    PERFORM pg_advisory_xact_lock(hashtextextended('reservation:' || NEW."ReservationId"::text, 0));
                    SELECT * INTO held FROM "Reservations" WHERE "Id" = NEW."ReservationId";
                    SELECT * INTO actor FROM "AspNetUsers" WHERE "Id" = NEW."ActorId" FOR SHARE;
                    SELECT * INTO previous FROM "ReservationEvents" WHERE "ReservationId" = NEW."ReservationId"
                        ORDER BY "Sequence" DESC LIMIT 1;
                    expected_agent := CASE WHEN previous."Id" IS NULL OR previous."Kind" = 'Released' THEN NULL
                        ELSE previous."AgentId" END;
                    IF held."Id" IS NULL OR NOT held."IsActive" OR actor."Id" IS NULL OR NOT actor."IsActive" OR
                       NEW."Sequence" <> COALESCE(previous."Sequence", 0) + 1 OR
                       NEW."PreviousAgentId" IS DISTINCT FROM expected_agent OR
                       NEW."RecordedAtUtc" > clock_timestamp() OR
                       (previous."Id" IS NOT NULL AND NEW."RecordedAtUtc" < previous."RecordedAtUtc") THEN
                        RAISE EXCEPTION 'A hold event must continue its reservation''s own history'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_ReservationEvents_History';
                    END IF;
                    IF (NEW."Kind" = 'Claimed' AND actor."Role" <> 'SalesAgent') OR
                       (NEW."Kind" IN ('Assigned', 'Reassigned') AND NOT
                           ((actor."Role" = 'BranchManager' AND actor."BranchId" IS NOT DISTINCT FROM held."BranchId") OR
                            actor."Role" = 'GeneralManager')) OR
                       (NEW."Kind" = 'Released' AND NOT
                           (actor."Id" = NEW."PreviousAgentId" OR
                            (actor."Role" = 'BranchManager' AND actor."BranchId" IS NOT DISTINCT FROM held."BranchId") OR
                            actor."Role" = 'GeneralManager')) THEN
                        RAISE EXCEPTION 'The actor may not make this change to the hold'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_ReservationEvents_Actor';
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER reservation_event_guard BEFORE INSERT ON "ReservationEvents"
                    FOR EACH ROW EXECUTE FUNCTION guard_reservation_event();

                CREATE FUNCTION require_reservation_claim_events() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE
                    reservation_id uuid;
                    held "Reservations"%ROWTYPE;
                    latest "ReservationEvents"%ROWTYPE;
                BEGIN
                    IF TG_TABLE_NAME = 'Reservations' THEN
                        reservation_id := NEW."Id";
                    ELSE
                        reservation_id := NEW."ReservationId";
                    END IF;
                    SELECT * INTO held FROM "Reservations" WHERE "Id" = reservation_id;
                    SELECT * INTO latest FROM "ReservationEvents" WHERE "ReservationId" = reservation_id
                        ORDER BY "Sequence" DESC LIMIT 1;
                    IF held."IsActive" AND (
                        (held."ClaimedById" IS NULL AND latest."Id" IS NOT NULL AND latest."Kind" <> 'Released') OR
                        (held."ClaimedById" IS NOT NULL AND (latest."Id" IS NULL OR latest."Kind" = 'Released' OR
                            latest."AgentId" IS DISTINCT FROM held."ClaimedById"))) THEN
                        RAISE EXCEPTION 'An active hold''s handler must agree with its retained claim events'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_Reservations_ClaimEvents';
                    END IF;
                    RETURN NULL;
                END $$;
                CREATE CONSTRAINT TRIGGER reservation_claim_consistency AFTER UPDATE ON "Reservations"
                    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION require_reservation_claim_events();
                CREATE CONSTRAINT TRIGGER reservation_event_consistency AFTER INSERT ON "ReservationEvents"
                    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION require_reservation_claim_events();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER reservation_event_consistency ON "ReservationEvents";
                DROP TRIGGER reservation_claim_consistency ON "Reservations";
                DROP FUNCTION require_reservation_claim_events();
                DROP TRIGGER reservation_event_guard ON "ReservationEvents";
                DROP FUNCTION guard_reservation_event();
                DROP TRIGGER reservation_events_append_only ON "ReservationEvents";
                DROP FUNCTION forbid_reservation_event_change();

                CREATE OR REPLACE FUNCTION guard_reservation() RETURNS trigger LANGUAGE plpgsql AS $$
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

                CREATE OR REPLACE FUNCTION guard_reservation_release() RETURNS trigger LANGUAGE plpgsql AS $$
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

                CREATE OR REPLACE FUNCTION release_unavailable_reservation(vehicle_id uuid) RETURNS void LANGUAGE plpgsql AS $$
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
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_Reservations_AspNetUsers_ClaimedById",
                table: "Reservations");

            migrationBuilder.DropTable(
                name: "ReservationEvents");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SalesOrderEvents_Shape",
                table: "SalesOrderEvents");

            migrationBuilder.DropIndex(
                name: "IX_Reservations_ClaimedById_ClaimedAt",
                table: "Reservations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Reservations_Claim",
                table: "Reservations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Reservations_Shape",
                table: "Reservations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ReservationReleases_Actor",
                table: "ReservationReleases");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ReservationReleases_Shape",
                table: "ReservationReleases");

            migrationBuilder.DropColumn(
                name: "ClaimedAt",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "ClaimedById",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "FrozenRemaining",
                table: "Reservations");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SalesOrderEvents_Shape",
                table: "SalesOrderEvents",
                sql: "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND isfinite(\"RecordedAtUtc\") AND \"Sequence\" > 0 AND \"Kind\" IN ('Created', 'Revised', 'Submitted', 'Approved', 'Rejected', 'Cancelled') AND \"ToStatus\" IN ('Draft', 'PendingManagerApproval', 'Approved', 'Cancelled') AND (\"FromStatus\" IS NULL OR \"FromStatus\" IN ('Draft', 'PendingManagerApproval', 'Approved', 'Cancelled')) AND ((\"Kind\" IN ('Rejected', 'Cancelled') AND \"Reason\" IS NOT NULL AND \"Reason\" = btrim(\"Reason\") AND length(\"Reason\") BETWEEN 1 AND 1000 AND \"Reason\" !~ '[[:cntrl:]]') OR (\"Kind\" NOT IN ('Rejected', 'Cancelled') AND \"Reason\" IS NULL))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Reservations_Shape",
                table: "Reservations",
                sql: "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND isfinite(\"CreatedAt\") AND isfinite(\"ExpiresAt\") AND \"ExpiresAt\" = \"CreatedAt\" + interval '48 hours' AND \"Deposit\" = 500");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ReservationReleases_Actor",
                table: "ReservationReleases",
                sql: "(\"Kind\" = 'CustomerCancelled' AND \"ActorId\" IS NOT NULL AND \"Reason\" IS NULL) OR (\"Kind\" = 'StaffCancelled' AND \"ActorId\" IS NOT NULL AND \"Reason\" IS NOT NULL AND length(btrim(\"Reason\")) BETWEEN 1 AND 1000 AND \"Reason\" = btrim(\"Reason\") AND \"Reason\" !~ '[[:cntrl:]]') OR (\"Kind\" IN ('Expired', 'Withdrawn', 'Reinspection') AND \"ActorId\" IS NULL AND \"Reason\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ReservationReleases_Shape",
                table: "ReservationReleases",
                sql: "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND isfinite(\"RecordedAt\") AND \"Refund\" = 500 AND \"Kind\" IN ('CustomerCancelled', 'StaffCancelled', 'Expired', 'Withdrawn', 'Reinspection')");
        }
    }
}

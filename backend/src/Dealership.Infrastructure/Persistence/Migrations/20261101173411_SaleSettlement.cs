using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dealership.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SaleSettlement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Vehicles_State",
                table: "Vehicles");

            migrationBuilder.DropIndex(
                name: "IX_SalesOrders_TradeInAppointmentId",
                table: "SalesOrders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SalesOrders_Approval",
                table: "SalesOrders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SalesOrders_Handling",
                table: "SalesOrders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SalesOrders_Shape",
                table: "SalesOrders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SalesOrderEvents_Shape",
                table: "SalesOrderEvents");

            migrationBuilder.AddColumn<Guid>(
                name: "AcquiredVehicleId",
                table: "SalesOrders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentEvidence",
                table: "SalesOrders",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SettledAt",
                table: "SalesOrders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Vehicles_State",
                table: "Vehicles",
                sql: "\"MileageKilometres\" >= 0 AND \"Status\" IN ('PendingInspection', 'Rejected', 'Available', 'Sold') AND \"ManufactureYear\" > 0");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_AcquiredVehicleId",
                table: "SalesOrders",
                column: "AcquiredVehicleId",
                unique: true,
                filter: "\"AcquiredVehicleId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_TradeInAppointmentId",
                table: "SalesOrders",
                column: "TradeInAppointmentId",
                unique: true,
                filter: "\"TradeInAppointmentId\" IS NOT NULL AND \"Status\" IN ('Open', 'Handling', 'PendingApproval', 'Approved', 'Settled')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SalesOrders_Approval",
                table: "SalesOrders",
                sql: "(\"Status\" = 'PendingApproval' AND \"DiscountLimitAtSubmit\" IS NOT NULL AND \"DecidedById\" IS NULL AND \"NegotiatedDiscount\" > \"DiscountLimitAtSubmit\") OR (\"Status\" IN ('Approved', 'Settled') AND \"DiscountLimitAtSubmit\" IS NOT NULL AND \"DecidedAt\" IS NOT NULL AND (\"DecidedById\" IS NOT NULL OR \"NegotiatedDiscount\" <= \"DiscountLimitAtSubmit\") AND \"DecidedById\" IS DISTINCT FROM \"AgentId\") OR (\"Status\" IN ('Open', 'Handling') AND \"DiscountLimitAtSubmit\" IS NULL AND \"DecidedById\" IS NULL) OR \"Status\" IN ('Expired', 'Cancelled')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SalesOrders_Handling",
                table: "SalesOrders",
                sql: "(\"Status\" = 'Open' AND \"DueAt\" IS NOT NULL AND \"AgentId\" IS NULL AND \"ClaimedAt\" IS NULL AND \"PausedSeconds\" IS NULL AND \"ClosedAt\" IS NULL) OR (\"Status\" IN ('Handling', 'PendingApproval', 'Approved') AND \"DueAt\" IS NULL AND \"AgentId\" IS NOT NULL AND \"ClaimedAt\" IS NOT NULL AND \"PausedSeconds\" BETWEEN 1 AND 172800 AND \"ClosedAt\" IS NULL) OR (\"Status\" IN ('Expired', 'Cancelled') AND \"AgentId\" IS NULL AND \"ClaimedAt\" IS NULL AND \"PausedSeconds\" IS NULL AND \"ClosedAt\" IS NOT NULL) OR (\"Status\" = 'Settled' AND \"DueAt\" IS NULL AND \"AgentId\" IS NOT NULL AND \"ClaimedAt\" IS NOT NULL AND \"PausedSeconds\" IS NULL AND \"ClosedAt\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SalesOrders_Settlement",
                table: "SalesOrders",
                sql: "(\"Status\" = 'Settled' AND \"PaymentEvidence\" IS NOT NULL AND \"SettledAt\" IS NOT NULL AND isfinite(\"SettledAt\") AND \"SettledAt\" >= \"DecidedAt\" AND \"SettledAt\" <= \"UpdatedAt\" AND (\"TradeInAppointmentId\" IS NULL) = (\"AcquiredVehicleId\" IS NULL) AND \"AcquiredVehicleId\" IS DISTINCT FROM \"VehicleId\") OR (\"Status\" <> 'Settled' AND \"PaymentEvidence\" IS NULL AND \"SettledAt\" IS NULL AND \"AcquiredVehicleId\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SalesOrders_Shape",
                table: "SalesOrders",
                sql: "\"Version\" > 0 AND \"Status\" IN ('Open', 'Handling', 'PendingApproval', 'Approved', 'Expired', 'Cancelled', 'Settled') AND isfinite(\"CreatedAt\") AND isfinite(\"UpdatedAt\") AND \"UpdatedAt\" >= \"CreatedAt\" AND ((\"OrderNumber\" IS NULL AND \"SubmittedAt\" IS NULL) OR (\"OrderNumber\" ~ '^VPO-[0-9]{4}-[0-9]{6}$' AND \"SubmittedAt\" IS NOT NULL))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SalesOrderEvents_Shape",
                table: "SalesOrderEvents",
                sql: "isfinite(\"RecordedAt\") AND \"Sequence\" > 0 AND \"Kind\" IN ('Created', 'Claimed', 'Assigned', 'Reassigned', 'Released', 'TermsSaved', 'Submitted', 'Approved', 'Rejected', 'Cancelled', 'CustomerCancelled', 'Expired', 'Withdrawn', 'Settled') AND \"ToStatus\" IN ('Open', 'Handling', 'PendingApproval', 'Approved', 'Expired', 'Cancelled', 'Settled') AND (\"FromStatus\" IS NULL OR \"FromStatus\" IN ('Open', 'Handling', 'PendingApproval', 'Approved', 'Expired', 'Cancelled', 'Settled')) AND (\"FromStatus\" IS NULL) = (\"Kind\" = 'Created') AND (\"ActorId\" IS NULL) = (\"Kind\" IN ('Expired', 'Withdrawn')) AND ((\"Kind\" IN ('Rejected', 'Cancelled', 'Withdrawn') AND \"Reason\" IS NOT NULL AND \"Reason\" = btrim(\"Reason\") AND length(\"Reason\") BETWEEN 1 AND 1000 AND \"Reason\" !~ '[[:cntrl:]]') OR (\"Kind\" NOT IN ('Rejected', 'Cancelled', 'Withdrawn') AND \"Reason\" IS NULL)) AND (\"ReleaseId\" IS NOT NULL) = (\"Kind\" IN ('Cancelled', 'CustomerCancelled', 'Expired', 'Withdrawn')) AND (\"Changes\" IS NULL OR (\"Kind\" IN ('TermsSaved', 'Released') AND jsonb_typeof(\"Changes\") = 'array')) AND jsonb_typeof(\"Snapshot\") = 'object' AND \"Snapshot\" ->> 'Status' = \"ToStatus\" AND (\"Snapshot\" ->> 'Version')::bigint = \"Sequence\"");

            migrationBuilder.AddForeignKey(
                name: "FK_SalesOrders_Vehicles_AcquiredVehicleId",
                table: "SalesOrders",
                column: "AcquiredVehicleId",
                principalTable: "Vehicles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
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
                           (EXISTS (SELECT 1 FROM "ReservationReleases" WHERE "ReservationId" = OLD."Id") OR
                               EXISTS (SELECT 1 FROM "SalesOrders" WHERE "Id" = OLD."Id" AND "Status" = 'Settled')) THEN
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
                """);
            migrationBuilder.Sql("""
                CREATE FUNCTION guard_sale_settlement() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE
                    payment jsonb;
                    received numeric;
                    financing numeric;
                    method integer;
                BEGIN
                    IF TG_OP <> 'UPDATE' THEN
                        RETURN NEW;
                    END IF;
                    IF OLD."Status" = 'Settled' THEN
                        IF (to_jsonb(NEW) - ARRAY['AgentId', 'ClaimedAt', 'UpdatedAt', 'Version']) IS DISTINCT FROM
                           (to_jsonb(OLD) - ARRAY['AgentId', 'ClaimedAt', 'UpdatedAt', 'Version']) THEN
                            RAISE EXCEPTION 'Settlement freezes the sale and its payment evidence'
                                USING ERRCODE = '23514', CONSTRAINT = 'CK_SalesOrders_SettledImmutable';
                        END IF;
                        RETURN NEW;
                    END IF;
                    IF NEW."Status" <> 'Settled' THEN
                        RETURN NEW;
                    END IF;
                    IF OLD."Status" <> 'Approved' OR NEW."SettledAt" <> NEW."UpdatedAt" OR
                       NEW."SettledAt" > clock_timestamp() OR
                       (to_jsonb(NEW) - ARRAY['Status', 'PaymentEvidence', 'SettledAt', 'AcquiredVehicleId',
                           'PausedSeconds', 'Version', 'UpdatedAt']) IS DISTINCT FROM
                       (to_jsonb(OLD) - ARRAY['Status', 'PaymentEvidence', 'SettledAt', 'AcquiredVehicleId',
                           'PausedSeconds', 'Version', 'UpdatedAt']) THEN
                        RAISE EXCEPTION 'Only approved unchanged terms may be settled'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_SalesOrders_SettlementTransition';
                    END IF;
                    PERFORM 1 FROM "AspNetUsers" WHERE "Id" = NEW."AgentId" AND "IsActive" AND
                        "Role" = 'SalesAgent' AND "BranchId" = NEW."BranchId" FOR SHARE;
                    IF NOT FOUND THEN
                        RAISE EXCEPTION 'Settlement requires the current eligible handling agent'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_SalesOrders_SettlementActor';
                    END IF;
                    PERFORM 1 FROM "Branches" WHERE "Id" = NEW."BranchId" AND "IsActive" FOR SHARE;
                    IF NOT FOUND THEN
                        RAISE EXCEPTION 'Settlement requires an active branch'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_SalesOrders_SettlementActor';
                    END IF;
                    PERFORM 1 FROM "Vehicles" WHERE "Id" = NEW."VehicleId" AND "Status" = 'Available' FOR UPDATE;
                    IF NOT FOUND OR NOT EXISTS (SELECT 1 FROM "Reservations" WHERE "Id" = NEW."Id" AND "IsActive") OR
                       EXISTS (SELECT 1 FROM "ReservationReleases" WHERE "ReservationId" = NEW."Id") THEN
                        RAISE EXCEPTION 'Settlement requires the retained eligible hold'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_SalesOrders_SettlementVehicle';
                    END IF;
                    IF NEW."TradeInAppointmentId" IS NOT NULL THEN
                        PERFORM 1 FROM "Vehicles" v JOIN "AppointmentCheckIns" c ON c."VehicleId" = v."Id"
                            JOIN "IntakeAppointments" a ON a."Id" = c."AppointmentId"
                            WHERE v."Id" = NEW."AcquiredVehicleId" AND c."AppointmentId" = NEW."TradeInAppointmentId"
                                AND c."OwnerId" = NEW."CustomerId" AND v."Id" <> NEW."VehicleId" AND
                                v."Status" <> 'Sold' AND NEW."TradeInAllowance" <= a."ValuationMaximum" FOR UPDATE OF v;
                        IF NOT FOUND THEN
                            RAISE EXCEPTION 'Acquisition requires this customer''s linked checked-in vehicle and allowance'
                                USING ERRCODE = '23514', CONSTRAINT = 'CK_SalesOrders_Acquisition';
                        END IF;
                    END IF;
                    payment := NEW."PaymentEvidence";
                    received := (payment->>'ReceivedAmount')::numeric;
                    financing := (payment->>'FinancingAmount')::numeric;
                    method := (payment->>'Method')::integer;
                    IF jsonb_typeof(payment) IS DISTINCT FROM 'object' OR
                       jsonb_typeof(payment->'Method') IS DISTINCT FROM 'number' OR
                       payment->'Method' NOT IN ('0'::jsonb, '1'::jsonb, '2'::jsonb, '3'::jsonb) OR
                       jsonb_typeof(payment->'ReceivedAmount') IS DISTINCT FROM 'number' OR
                       jsonb_typeof(payment->'FinancingAmount') IS DISTINCT FROM 'number' OR
                       NOT COALESCE(received BETWEEN 0 AND 9999999999999999.99 AND received = trunc(received, 2) AND
                           financing BETWEEN 0 AND 9999999999999999.99 AND financing = trunc(financing, 2) AND
                           received + financing = NEW."BalancePayable", false) OR
                       NOT COALESCE(
                           (NEW."BalancePayable" = 0 AND method = 3 AND payment->'ZeroBalanceConfirmed' = 'true'::jsonb AND
                               received = 0 AND financing = 0 AND payment->'PaymentReference' = 'null'::jsonb AND
                               payment->'PaymentDate' = 'null'::jsonb AND payment->'Bank' = 'null'::jsonb AND
                               payment->'LouReference' = 'null'::jsonb AND payment->'LouVerified' = 'false'::jsonb) OR
                           (NEW."BalancePayable" > 0 AND payment->'ZeroBalanceConfirmed' = 'false'::jsonb AND (
                               (method IN (0, 1) AND financing = 0 AND
                                   jsonb_typeof(payment->'PaymentReference') = 'string' AND
                                   length(payment->>'PaymentReference') BETWEEN 1 AND 1000 AND
                                   payment->>'PaymentReference' = btrim(payment->>'PaymentReference') AND
                                   payment->>'PaymentReference' !~ '[[:cntrl:]]' AND
                                   jsonb_typeof(payment->'PaymentDate') = 'string' AND
                                   (payment->>'PaymentDate')::date BETWEEN date '0001-01-01' AND date '9999-12-31' AND
                                   payment->>'PaymentDate' = to_char((payment->>'PaymentDate')::date, 'YYYY-MM-DD') AND
                                   payment->'Bank' = 'null'::jsonb AND payment->'LouReference' = 'null'::jsonb AND
                                   payment->'LouVerified' = 'false'::jsonb) OR
                               (method = 2 AND financing > 0 AND jsonb_typeof(payment->'Bank') = 'string' AND
                                   length(payment->>'Bank') BETWEEN 1 AND 100 AND
                                   payment->>'Bank' = btrim(payment->>'Bank') AND payment->>'Bank' !~ '[[:cntrl:]]' AND
                                   jsonb_typeof(payment->'LouReference') = 'string' AND
                                   length(payment->>'LouReference') BETWEEN 1 AND 1000 AND
                                   payment->>'LouReference' = btrim(payment->>'LouReference') AND
                                   payment->>'LouReference' !~ '[[:cntrl:]]' AND
                                   payment->'LouVerified' = 'true'::jsonb AND payment->'PaymentReference' = 'null'::jsonb AND
                                   payment->'PaymentDate' = 'null'::jsonb))), false) THEN
                        RAISE EXCEPTION 'Manual evidence must exactly cover the approved balance'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_SalesOrders_Payment';
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER sale_settlement_guard BEFORE UPDATE ON "SalesOrders"
                    FOR EACH ROW EXECUTE FUNCTION guard_sale_settlement();

                CREATE FUNCTION apply_sale_settlement() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF NEW."Status" = 'Settled' AND OLD."Status" <> 'Settled' THEN
                        UPDATE "Reservations" SET "IsActive" = false WHERE "Id" = NEW."Id";
                        UPDATE "Vehicles" SET "Status" = 'Sold' WHERE "Id" = NEW."VehicleId";
                    END IF;
                    RETURN NULL;
                END $$;
                CREATE TRIGGER sale_settled AFTER UPDATE ON "SalesOrders"
                    FOR EACH ROW EXECUTE FUNCTION apply_sale_settlement();

                CREATE FUNCTION require_sale_settlement() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF NEW."Status" = 'Settled' AND OLD."Status" <> 'Settled' AND NOT EXISTS (
                        SELECT 1 FROM "SalesOrderEvents" e WHERE e."SalesOrderId" = NEW."Id" AND
                            e."Sequence" = NEW."Version" AND e."Kind" = 'Settled' AND e."FromStatus" = 'Approved' AND
                            e."ActorId" = NEW."AgentId" AND e."RecordedAt" = NEW."SettledAt" AND
                            e."Snapshot"->'PaymentEvidence' = NEW."PaymentEvidence" AND
                            e."Snapshot"->>'AcquiredVehicleId' IS NOT DISTINCT FROM NEW."AcquiredVehicleId"::text) THEN
                        RAISE EXCEPTION 'Settlement requires its exact immutable handling-agent receipt'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_SalesOrders_SettlementReceipt';
                    END IF;
                    RETURN NULL;
                END $$;
                CREATE CONSTRAINT TRIGGER sale_settlement_receipt AFTER UPDATE ON "SalesOrders"
                    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION require_sale_settlement();

                CREATE OR REPLACE FUNCTION require_reservation_release() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "Reservations" r WHERE r."Id" = NEW."Id" AND r."IsActive" =
                        (EXISTS (SELECT 1 FROM "ReservationReleases" WHERE "ReservationId" = r."Id") OR
                         EXISTS (SELECT 1 FROM "SalesOrders" WHERE "Id" = r."Id" AND "Status" = 'Settled'))) THEN
                        RAISE EXCEPTION 'Reservation slots must agree with release or settlement evidence'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_Reservations_Release';
                    END IF;
                    RETURN NULL;
                END $$;

                CREATE OR REPLACE FUNCTION inspection_vehicle_status(vehicle_id uuid) RETURNS text LANGUAGE plpgsql AS $$
                DECLARE
                    draft "InspectionDrafts"%ROWTYPE;
                    report jsonb;
                    request "InspectionRepairRequests"%ROWTYPE;
                BEGIN
                    IF EXISTS (SELECT 1 FROM "SalesOrders" WHERE "VehicleId" = vehicle_id AND "Status" = 'Settled') THEN
                        RETURN 'Sold';
                    END IF;
                    SELECT * INTO draft FROM "InspectionDrafts" WHERE "VehicleId" = vehicle_id;
                    IF draft."Id" IS NULL OR NOT draft."IsSigned" THEN
                        RETURN 'PendingInspection';
                    END IF;
                    SELECT "ReportJson" INTO report FROM "InspectionSignedRevisions"
                        WHERE "InspectionId" = draft."Id" AND "Revision" = draft."Revision";
                    IF report IS NULL THEN
                        RETURN 'PendingInspection';
                    END IF;
                    IF report->'assessment'->'isRejected' = 'true'::jsonb THEN
                        RETURN 'Rejected';
                    END IF;
                    SELECT * INTO request FROM "InspectionRepairRequests" WHERE "InspectionId" = draft."Id"
                        AND "Revision" = draft."Revision" AND "Version" = draft."RepairVersion";
                    IF request."Id" IS NOT NULL AND NOT EXISTS (SELECT 1 FROM "InspectionRepairCompletions" c
                        WHERE c."RequestId" = request."Id" AND c."InspectionId" = draft."Id" AND
                            c."Revision" = draft."Revision" AND c."AssignmentVersion" = draft."AssignmentVersion" AND
                            c."RepairVersion" = draft."RepairVersion") THEN
                        RETURN 'PendingInspection';
                    END IF;
                    -- Every required finding must be in the completed current request, even when its estimate is zero.
                    IF EXISTS (SELECT 1 FROM jsonb_array_elements(report->'checklist') point
                        WHERE point->'kind' = '0'::jsonb AND
                            (point->'severity' IN ('1'::jsonb, '2'::jsonb) OR (point->>'estimatedRepairCost')::numeric > 0) AND
                            NOT EXISTS (SELECT 1 FROM jsonb_array_elements(request."ItemsJson") item
                                WHERE item->>'pointCode' = point->>'code')) THEN
                        RETURN 'PendingInspection';
                    END IF;
                    RETURN 'Available';
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "SalesOrders" WHERE "Status" = 'Settled') THEN
                        RAISE EXCEPTION 'Cannot remove retained settlement evidence';
                    END IF;
                END $$;
                DROP TRIGGER sale_settlement_guard ON "SalesOrders";
                DROP FUNCTION guard_sale_settlement();
                DROP TRIGGER sale_settled ON "SalesOrders";
                DROP FUNCTION apply_sale_settlement();
                DROP TRIGGER sale_settlement_receipt ON "SalesOrders";
                DROP FUNCTION require_sale_settlement();
                CREATE OR REPLACE FUNCTION inspection_vehicle_status(vehicle_id uuid) RETURNS text LANGUAGE plpgsql AS $$
                DECLARE
                    draft "InspectionDrafts"%ROWTYPE;
                    report jsonb;
                    request "InspectionRepairRequests"%ROWTYPE;
                BEGIN
                    SELECT * INTO draft FROM "InspectionDrafts" WHERE "VehicleId" = vehicle_id;
                    IF draft."Id" IS NULL OR NOT draft."IsSigned" THEN
                        RETURN 'PendingInspection';
                    END IF;
                    SELECT "ReportJson" INTO report FROM "InspectionSignedRevisions"
                        WHERE "InspectionId" = draft."Id" AND "Revision" = draft."Revision";
                    IF report IS NULL THEN
                        RETURN 'PendingInspection';
                    END IF;
                    IF report->'assessment'->'isRejected' = 'true'::jsonb THEN
                        RETURN 'Rejected';
                    END IF;
                    SELECT * INTO request FROM "InspectionRepairRequests" WHERE "InspectionId" = draft."Id"
                        AND "Revision" = draft."Revision" AND "Version" = draft."RepairVersion";
                    IF request."Id" IS NOT NULL AND NOT EXISTS (SELECT 1 FROM "InspectionRepairCompletions" c
                        WHERE c."RequestId" = request."Id" AND c."InspectionId" = draft."Id" AND
                            c."Revision" = draft."Revision" AND c."AssignmentVersion" = draft."AssignmentVersion" AND
                            c."RepairVersion" = draft."RepairVersion") THEN
                        RETURN 'PendingInspection';
                    END IF;
                    -- Every required finding must be in the completed current request, even when its estimate is zero.
                    IF EXISTS (SELECT 1 FROM jsonb_array_elements(report->'checklist') point
                        WHERE point->'kind' = '0'::jsonb AND
                            (point->'severity' IN ('1'::jsonb, '2'::jsonb) OR (point->>'estimatedRepairCost')::numeric > 0) AND
                            NOT EXISTS (SELECT 1 FROM jsonb_array_elements(request."ItemsJson") item
                                WHERE item->>'pointCode' = point->>'code')) THEN
                        RETURN 'PendingInspection';
                    END IF;
                    RETURN 'Available';
                END $$;
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
                CREATE OR REPLACE FUNCTION require_reservation_release() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "Reservations" r WHERE r."Id" = NEW."Id" AND
                        r."IsActive" = EXISTS (SELECT 1 FROM "ReservationReleases" WHERE "ReservationId" = r."Id")) THEN
                        RAISE EXCEPTION 'Active reservation slots must agree with retained release receipts'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_Reservations_Release';
                    END IF;
                    RETURN NULL;
                END $$;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_SalesOrders_Vehicles_AcquiredVehicleId",
                table: "SalesOrders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Vehicles_State",
                table: "Vehicles");

            migrationBuilder.DropIndex(
                name: "IX_SalesOrders_AcquiredVehicleId",
                table: "SalesOrders");

            migrationBuilder.DropIndex(
                name: "IX_SalesOrders_TradeInAppointmentId",
                table: "SalesOrders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SalesOrders_Approval",
                table: "SalesOrders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SalesOrders_Handling",
                table: "SalesOrders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SalesOrders_Settlement",
                table: "SalesOrders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SalesOrders_Shape",
                table: "SalesOrders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SalesOrderEvents_Shape",
                table: "SalesOrderEvents");

            migrationBuilder.DropColumn(
                name: "AcquiredVehicleId",
                table: "SalesOrders");

            migrationBuilder.DropColumn(
                name: "PaymentEvidence",
                table: "SalesOrders");

            migrationBuilder.DropColumn(
                name: "SettledAt",
                table: "SalesOrders");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Vehicles_State",
                table: "Vehicles",
                sql: "\"MileageKilometres\" >= 0 AND \"Status\" IN ('PendingInspection', 'Rejected', 'Available') AND \"ManufactureYear\" > 0");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_TradeInAppointmentId",
                table: "SalesOrders",
                column: "TradeInAppointmentId",
                unique: true,
                filter: "\"TradeInAppointmentId\" IS NOT NULL AND \"Status\" IN ('Open', 'Handling', 'PendingApproval', 'Approved')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SalesOrders_Approval",
                table: "SalesOrders",
                sql: "(\"Status\" = 'PendingApproval' AND \"DiscountLimitAtSubmit\" IS NOT NULL AND \"DecidedById\" IS NULL AND \"NegotiatedDiscount\" > \"DiscountLimitAtSubmit\") OR (\"Status\" = 'Approved' AND \"DiscountLimitAtSubmit\" IS NOT NULL AND \"DecidedAt\" IS NOT NULL AND (\"DecidedById\" IS NOT NULL OR \"NegotiatedDiscount\" <= \"DiscountLimitAtSubmit\") AND \"DecidedById\" IS DISTINCT FROM \"AgentId\") OR (\"Status\" IN ('Open', 'Handling') AND \"DiscountLimitAtSubmit\" IS NULL AND \"DecidedById\" IS NULL) OR \"Status\" IN ('Expired', 'Cancelled')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SalesOrders_Handling",
                table: "SalesOrders",
                sql: "(\"Status\" = 'Open' AND \"DueAt\" IS NOT NULL AND \"AgentId\" IS NULL AND \"ClaimedAt\" IS NULL AND \"PausedSeconds\" IS NULL AND \"ClosedAt\" IS NULL) OR (\"Status\" IN ('Handling', 'PendingApproval', 'Approved') AND \"DueAt\" IS NULL AND \"AgentId\" IS NOT NULL AND \"ClaimedAt\" IS NOT NULL AND \"PausedSeconds\" BETWEEN 1 AND 172800 AND \"ClosedAt\" IS NULL) OR (\"Status\" IN ('Expired', 'Cancelled') AND \"AgentId\" IS NULL AND \"ClaimedAt\" IS NULL AND \"PausedSeconds\" IS NULL AND \"ClosedAt\" IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SalesOrders_Shape",
                table: "SalesOrders",
                sql: "\"Version\" > 0 AND \"Status\" IN ('Open', 'Handling', 'PendingApproval', 'Approved', 'Expired', 'Cancelled') AND isfinite(\"CreatedAt\") AND isfinite(\"UpdatedAt\") AND \"UpdatedAt\" >= \"CreatedAt\" AND ((\"OrderNumber\" IS NULL AND \"SubmittedAt\" IS NULL) OR (\"OrderNumber\" ~ '^VPO-[0-9]{4}-[0-9]{6}$' AND \"SubmittedAt\" IS NOT NULL))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SalesOrderEvents_Shape",
                table: "SalesOrderEvents",
                sql: "isfinite(\"RecordedAt\") AND \"Sequence\" > 0 AND \"Kind\" IN ('Created', 'Claimed', 'Assigned', 'Reassigned', 'Released', 'TermsSaved', 'Submitted', 'Approved', 'Rejected', 'Cancelled', 'CustomerCancelled', 'Expired', 'Withdrawn') AND \"ToStatus\" IN ('Open', 'Handling', 'PendingApproval', 'Approved', 'Expired', 'Cancelled') AND (\"FromStatus\" IS NULL OR \"FromStatus\" IN ('Open', 'Handling', 'PendingApproval', 'Approved', 'Expired', 'Cancelled')) AND (\"FromStatus\" IS NULL) = (\"Kind\" = 'Created') AND (\"ActorId\" IS NULL) = (\"Kind\" IN ('Expired', 'Withdrawn')) AND ((\"Kind\" IN ('Rejected', 'Cancelled', 'Withdrawn') AND \"Reason\" IS NOT NULL AND \"Reason\" = btrim(\"Reason\") AND length(\"Reason\") BETWEEN 1 AND 1000 AND \"Reason\" !~ '[[:cntrl:]]') OR (\"Kind\" NOT IN ('Rejected', 'Cancelled', 'Withdrawn') AND \"Reason\" IS NULL)) AND (\"ReleaseId\" IS NOT NULL) = (\"Kind\" IN ('Cancelled', 'CustomerCancelled', 'Expired', 'Withdrawn')) AND (\"Changes\" IS NULL OR (\"Kind\" IN ('TermsSaved', 'Released') AND jsonb_typeof(\"Changes\") = 'array')) AND jsonb_typeof(\"Snapshot\") = 'object' AND \"Snapshot\" ->> 'Status' = \"ToStatus\" AND (\"Snapshot\" ->> 'Version')::bigint = \"Sequence\"");
        }
    }
}

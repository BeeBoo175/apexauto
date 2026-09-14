using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dealership.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RetentionSaleDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_SaleDocumentCorrections_Shape",
                table: "SaleDocumentCorrections");

            migrationBuilder.AddColumn<Guid>(
                name: "RetentionHoldById",
                table: "SalesOrders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RetentionHoldReason",
                table: "SalesOrders",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RetentionHoldUntil",
                table: "SalesOrders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Reason",
                table: "SaleDocumentCorrections",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000);

            migrationBuilder.AlterColumn<string>(
                name: "BuyerName",
                table: "SaleDocumentCorrections",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "BuyerAddress",
                table: "SaleDocumentCorrections",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "MinimizedAt",
                table: "SaleDocumentCorrections",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_RetentionHoldById",
                table: "SalesOrders",
                column: "RetentionHoldById");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SalesOrders_Hold",
                table: "SalesOrders",
                sql: "(\"RetentionHoldUntil\" IS NULL AND \"RetentionHoldReason\" IS NULL AND \"RetentionHoldById\" IS NULL) OR (\"RetentionHoldUntil\" IS NOT NULL AND \"RetentionHoldById\" IS NOT NULL AND isfinite(\"RetentionHoldUntil\") AND \"RetentionHoldReason\" IS NOT NULL AND length(\"RetentionHoldReason\") BETWEEN 1 AND 1000 AND \"RetentionHoldReason\" = btrim(\"RetentionHoldReason\") AND \"RetentionHoldReason\" !~ '[[:cntrl:]]')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SaleDocumentCorrections_Shape",
                table: "SaleDocumentCorrections",
                sql: "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"ExpectedVersion\" > 0 AND isfinite(\"RecordedAt\") AND ((\"MinimizedAt\" IS NULL AND \"BuyerName\" IS NOT NULL AND \"BuyerAddress\" IS NOT NULL AND \"Reason\" IS NOT NULL AND length(\"BuyerName\") BETWEEN 1 AND 200 AND length(\"BuyerAddress\") BETWEEN 1 AND 1000 AND length(\"Reason\") BETWEEN 1 AND 1000 AND \"BuyerName\" = btrim(\"BuyerName\") AND \"BuyerName\" ~ '[^[:space:]]' AND \"BuyerName\" !~ '[[:cntrl:]]' AND \"BuyerAddress\" = btrim(\"BuyerAddress\") AND \"BuyerAddress\" ~ '[^[:space:]]' AND \"BuyerAddress\" !~ '[[:cntrl:]]' AND \"Reason\" = btrim(\"Reason\") AND \"Reason\" ~ '[^[:space:]]' AND \"Reason\" !~ '[[:cntrl:]]') OR (\"MinimizedAt\" IS NOT NULL AND \"BuyerName\" IS NULL AND \"BuyerAddress\" IS NULL AND \"Reason\" IS NULL))");

            migrationBuilder.AddForeignKey(
                name: "FK_SalesOrders_AspNetUsers_RetentionHoldById",
                table: "SalesOrders",
                column: "RetentionHoldById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql("""
                -- A preservation hold may change its own columns of a sale of any state; the three guards and the handover receipt
                -- check below ignore those columns and keep every other rule, including the version and history of the sale.
                CREATE OR REPLACE FUNCTION guard_sale_settlement() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE
                    payment jsonb;
                    received numeric;
                    financing numeric;
                    method integer;
                BEGIN
                    IF TG_OP <> 'UPDATE' THEN
                        RETURN NEW;
                    END IF;
                    -- A preservation hold changes only its own columns and leaves the sale's version and history alone.
                    IF NEW."Version" = OLD."Version" AND NEW."UpdatedAt" = OLD."UpdatedAt" AND
                       ROW(NEW."RetentionHoldUntil", NEW."RetentionHoldReason", NEW."RetentionHoldById") IS DISTINCT FROM
                           ROW(OLD."RetentionHoldUntil", OLD."RetentionHoldReason", OLD."RetentionHoldById") AND
                       (to_jsonb(NEW) - ARRAY['RetentionHoldUntil', 'RetentionHoldReason', 'RetentionHoldById']) =
                       (to_jsonb(OLD) - ARRAY['RetentionHoldUntil', 'RetentionHoldReason', 'RetentionHoldById']) THEN
                        RETURN NEW;
                    END IF;
                    IF OLD."Status" = 'Delivered' THEN
                        RAISE EXCEPTION 'Delivered sales are final'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_SalesOrders_DeliveredImmutable';
                    END IF;
                    IF OLD."Status" = 'Settled' THEN
                        IF NEW."Status" NOT IN ('Settled', 'Delivered') OR (to_jsonb(NEW) - ARRAY['AgentId', 'ClaimedAt', 'UpdatedAt', 'Version', 'Status', 'HandoverEvidence', 'DeliveredAt', 'ClosedAt']) IS DISTINCT FROM
                           (to_jsonb(OLD) - ARRAY['AgentId', 'ClaimedAt', 'UpdatedAt', 'Version', 'Status', 'HandoverEvidence', 'DeliveredAt', 'ClosedAt']) THEN
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
                CREATE OR REPLACE FUNCTION guard_sales_order() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE
                    held "Reservations"%ROWTYPE;
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        RAISE EXCEPTION 'Sales are retained'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_SalesOrders_Immutable';
                    END IF;
                    IF TG_OP = 'INSERT' THEN
                        SELECT * INTO held FROM "Reservations" WHERE "Id" = NEW."Id";
                        IF held."Id" IS NULL OR NEW."Status" <> 'Open' OR NEW."Version" <> 1 OR
                           NEW."DueAt" IS DISTINCT FROM held."ExpiresAt" OR NEW."CreatedAt" <> held."CreatedAt" OR
                           ROW(NEW."BasePrice", NEW."RoadTax", NEW."ProcessingFee", NEW."SstRate", NEW."PromoId",
                               NEW."PromoDiscount", NEW."DepositCredit", NEW."NegotiatedDiscount", NEW."WarrantyPrice",
                               NEW."TradeInAllowance") IS DISTINCT FROM
                           ROW(held."VehiclePrice", held."RoadTax", held."ProcessingFee", held."SstRate", held."PromoId",
                               held."PromoDiscount", held."Deposit", 0::numeric, 0::numeric, 0::numeric) THEN
                            RAISE EXCEPTION 'A sale opens with its reservation''s confirmed terms and deadline'
                                USING ERRCODE = '23514', CONSTRAINT = 'CK_SalesOrders_Origin';
                        END IF;
                        RETURN NEW;
                    END IF;
                    -- A preservation hold changes only its own columns and leaves the sale's version and history alone.
                    IF NEW."Version" = OLD."Version" AND NEW."UpdatedAt" = OLD."UpdatedAt" AND
                       ROW(NEW."RetentionHoldUntil", NEW."RetentionHoldReason", NEW."RetentionHoldById") IS DISTINCT FROM
                           ROW(OLD."RetentionHoldUntil", OLD."RetentionHoldReason", OLD."RetentionHoldById") AND
                       (to_jsonb(NEW) - ARRAY['RetentionHoldUntil', 'RetentionHoldReason', 'RetentionHoldById']) =
                       (to_jsonb(OLD) - ARRAY['RetentionHoldUntil', 'RetentionHoldReason', 'RetentionHoldById']) THEN
                        RETURN NEW;
                    END IF;
                    IF OLD."Status" IN ('Expired', 'Cancelled') THEN
                        RAISE EXCEPTION 'A closed sale is final'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_SalesOrders_Immutable';
                    END IF;
                    IF ROW(NEW."Id", NEW."VehicleId", NEW."BranchId", NEW."CustomerId", NEW."CreatedAt", NEW."BasePrice",
                           NEW."RoadTax", NEW."PromoId", NEW."PromoCode", NEW."PromoDiscount", NEW."DepositCredit")
                       IS DISTINCT FROM
                       ROW(OLD."Id", OLD."VehicleId", OLD."BranchId", OLD."CustomerId", OLD."CreatedAt", OLD."BasePrice",
                           OLD."RoadTax", OLD."PromoId", OLD."PromoCode", OLD."PromoDiscount", OLD."DepositCredit") OR
                       NEW."Version" <> OLD."Version" + 1 OR NEW."UpdatedAt" < OLD."UpdatedAt" OR
                       (OLD."OrderNumber" IS NOT NULL AND NEW."OrderNumber" IS DISTINCT FROM OLD."OrderNumber") THEN
                        RAISE EXCEPTION 'A sale keeps its origin and number and advances one version at a time'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_SalesOrders_Immutable';
                    END IF;
                    IF NEW."AgentId" IS NOT NULL AND NEW."AgentId" IS DISTINCT FROM OLD."AgentId" THEN
                        PERFORM 1 FROM "AspNetUsers" WHERE "Id" = NEW."AgentId" AND "IsActive" AND
                            "Role" = 'SalesAgent' AND "BranchId" = NEW."BranchId" FOR SHARE;
                        IF NOT FOUND THEN
                            RAISE EXCEPTION 'A sale is handled by an active sales agent of its branch'
                                USING ERRCODE = '23514', CONSTRAINT = 'CK_SalesOrders_Agent';
                        END IF;
                        PERFORM 1 FROM "Branches" WHERE "Id" = NEW."BranchId" AND "IsActive" FOR SHARE;
                        IF NOT FOUND THEN
                            RAISE EXCEPTION 'A sale is handled only in an active branch'
                                USING ERRCODE = '23514', CONSTRAINT = 'CK_SalesOrders_Agent';
                        END IF;
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE OR REPLACE FUNCTION require_sale_handover() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF NEW."Status" = 'Delivered' AND OLD."Status" <> 'Delivered' AND NOT EXISTS (
                        SELECT 1 FROM "SalesOrderEvents" e JOIN "SaleDocuments" d
                            ON d."IssuingOperationId" = e."Id" AND d."SalesOrderId" = e."SalesOrderId"
                            JOIN "SaleDeliveryMailOutbox" m ON m."DeliveryOperationId" = e."Id" AND m."SalesOrderId" = e."SalesOrderId"
                        WHERE e."SalesOrderId" = NEW."Id" AND e."Sequence" = NEW."Version" AND
                            e."Kind" = 'Delivered' AND e."FromStatus" = 'Settled' AND e."ToStatus" = 'Delivered' AND
                            e."ActorId" = NEW."AgentId" AND e."RecordedAt" = NEW."DeliveredAt" AND
                            e."Snapshot" = to_jsonb(NEW) - ARRAY['Id', 'VehicleId', 'BranchId', 'CustomerId',
                                'PromoId', 'CreatedAt', 'UpdatedAt',
                                'RetentionHoldUntil', 'RetentionHoldReason', 'RetentionHoldById'] AND d."Kind" = 'HandoverNote' AND
                            d."IssuedAt" = NEW."DeliveredAt") THEN
                        RAISE EXCEPTION 'Delivery requires its exact history, handover note and durable mail intent'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_SalesOrders_HandoverReceipt';
                    END IF;
                    RETURN NULL;
                END $$;
                CREATE OR REPLACE FUNCTION guard_sale_document_correction() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE
                    actor "AspNetUsers"%ROWTYPE;
                    sale "SalesOrders"%ROWTYPE;
                    original "SaleDocuments"%ROWTYPE;
                BEGIN
                    -- Retention may remove the buyer spelling and the reason of a correction of a delivered sale, nothing else.
                    IF TG_OP = 'UPDATE' AND OLD."MinimizedAt" IS NULL AND NEW."MinimizedAt" IS NOT NULL AND
                       NEW."BuyerName" IS NULL AND NEW."BuyerAddress" IS NULL AND NEW."Reason" IS NULL AND
                       (to_jsonb(NEW) - ARRAY['BuyerName', 'BuyerAddress', 'Reason', 'MinimizedAt']) =
                       (to_jsonb(OLD) - ARRAY['BuyerName', 'BuyerAddress', 'Reason', 'MinimizedAt']) AND
                       EXISTS (SELECT 1 FROM "SalesOrders" WHERE "Id" = OLD."SalesOrderId" AND "Status" = 'Delivered') THEN
                        RETURN NEW;
                    END IF;
                    IF TG_OP <> 'INSERT' THEN
                        RAISE EXCEPTION 'Document correction evidence is append-only'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_SaleDocumentCorrections_Immutable';
                    END IF;
                    SELECT * INTO actor FROM "AspNetUsers" WHERE "Id" = NEW."ActorId" FOR SHARE;
                    SELECT * INTO sale FROM "SalesOrders" WHERE "Id" = NEW."SalesOrderId" FOR UPDATE;
                    SELECT * INTO original FROM "SaleDocuments" WHERE "Id" = NEW."OriginalDocumentId";
                    PERFORM 1 FROM "Branches" WHERE "Id" = sale."BranchId" AND "IsActive" FOR SHARE;
                    IF NOT FOUND OR NOT COALESCE(actor."IsActive" AND
                        (actor."Role" = 'GeneralManager' OR
                            (actor."Role" = 'BranchManager' AND actor."BranchId" = sale."BranchId")) AND
                        sale."Status" IN ('Settled', 'Delivered') AND NEW."ExpectedVersion" = sale."Version" AND
                        original."SalesOrderId" = sale."Id" AND original."ContentsJson"->'Review'->'BuyerName' IS NOT NULL AND
                            NEW."RecordedAt" >= original."IssuedAt" AND
                        NEW."RecordedAt" <= clock_timestamp() AND
                        (NEW."BuyerName" IS DISTINCT FROM original."ContentsJson"->'Review'->>'BuyerName' OR
                         NEW."BuyerAddress" IS DISTINCT FROM original."ContentsJson"->'Review'->>'BuyerAddress') AND
                        NOT EXISTS (SELECT 1 FROM "SalesOrderEvents" WHERE "Id" = NEW."Id"), false) THEN
                        RAISE EXCEPTION 'Correction requires current manager scope, version and changed buyer spelling'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_SaleDocumentCorrections_Scope';
                    END IF;
                    RETURN NEW;
                END $$;

                -- The buyer's name, NRIC and address may leave the settlement review of a delivered sale and its issued documents;
                -- no other column and no other key changes, and deleting stays refused.
                CREATE FUNCTION public.guard_sales_order_event_minimization() RETURNS trigger
                LANGUAGE plpgsql SET search_path = pg_catalog AS $$
                BEGIN
                    IF OLD."Kind" = 'Settled' AND OLD."SettlementReview" IS NOT NULL AND
                       (to_jsonb(NEW) - 'SettlementReview') = (to_jsonb(OLD) - 'SettlementReview') AND
                       NEW."SettlementReview" = (OLD."SettlementReview" - 'BuyerName' - 'BuyerNric' - 'BuyerAddress') AND
                       EXISTS (SELECT 1 FROM public."SalesOrders" s WHERE s."Id" = OLD."SalesOrderId" AND s."Status" = 'Delivered') THEN
                        RETURN NEW;
                    END IF;
                    RAISE EXCEPTION 'Sale history is append-only'
                        USING ERRCODE = '23514', CONSTRAINT = 'CK_SalesOrderEvents_Immutable';
                END $$;
                DROP TRIGGER sales_order_events_append_only ON "SalesOrderEvents";
                CREATE TRIGGER sales_order_events_append_only BEFORE DELETE ON "SalesOrderEvents"
                    FOR EACH ROW EXECUTE FUNCTION forbid_sales_order_event_change();
                CREATE TRIGGER sales_order_events_minimization BEFORE UPDATE ON "SalesOrderEvents"
                    FOR EACH ROW EXECUTE FUNCTION public.guard_sales_order_event_minimization();

                CREATE FUNCTION public.guard_sale_document_minimization() RETURNS trigger
                LANGUAGE plpgsql SET search_path = pg_catalog AS $$
                BEGIN
                    IF OLD."ContentsJson"->'Review'->'BuyerName' IS NOT NULL AND
                       (to_jsonb(NEW) - 'ContentsJson') = (to_jsonb(OLD) - 'ContentsJson') AND
                       NEW."ContentsJson" = jsonb_set(OLD."ContentsJson", ARRAY['Review'],
                           (OLD."ContentsJson"->'Review') - 'BuyerName' - 'BuyerNric' - 'BuyerAddress') AND
                       EXISTS (SELECT 1 FROM public."SalesOrders" s WHERE s."Id" = OLD."SalesOrderId" AND s."Status" = 'Delivered') THEN
                        RETURN NEW;
                    END IF;
                    RAISE EXCEPTION 'Issued sales documents are immutable'
                        USING ERRCODE = '23514', CONSTRAINT = 'CK_SaleDocuments_Immutable';
                END $$;
                DROP TRIGGER sale_document_guard ON "SaleDocuments";
                CREATE TRIGGER sale_document_guard BEFORE DELETE ON "SaleDocuments"
                    FOR EACH ROW EXECUTE FUNCTION guard_sale_document();
                CREATE TRIGGER sale_document_minimization BEFORE UPDATE ON "SaleDocuments"
                    FOR EACH ROW EXECUTE FUNCTION public.guard_sale_document_minimization();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "SaleDocumentCorrections" WHERE "MinimizedAt" IS NOT NULL) OR
                       EXISTS (SELECT 1 FROM "SaleDocuments" WHERE "ContentsJson"->'Review' IS NOT NULL AND
                           "ContentsJson"->'Review'->'BuyerName' IS NULL) OR
                       EXISTS (SELECT 1 FROM "SalesOrderEvents" WHERE "Kind" = 'Settled' AND "SettlementReview" IS NOT NULL AND
                           "SettlementReview"->'BuyerName' IS NULL) OR
                       EXISTS (SELECT 1 FROM "SalesOrders" WHERE "RetentionHoldUntil" IS NOT NULL) THEN
                        RAISE EXCEPTION 'Cannot revert RetentionSaleDocuments while minimized sale records or holds exist.';
                    END IF;
                END $$;
                DROP TRIGGER sales_order_events_minimization ON "SalesOrderEvents";
                DROP FUNCTION public.guard_sales_order_event_minimization();
                DROP TRIGGER sales_order_events_append_only ON "SalesOrderEvents";
                CREATE TRIGGER sales_order_events_append_only BEFORE UPDATE OR DELETE ON "SalesOrderEvents"
                    FOR EACH ROW EXECUTE FUNCTION forbid_sales_order_event_change();
                DROP TRIGGER sale_document_minimization ON "SaleDocuments";
                DROP FUNCTION public.guard_sale_document_minimization();
                DROP TRIGGER sale_document_guard ON "SaleDocuments";
                CREATE TRIGGER sale_document_guard BEFORE UPDATE OR DELETE ON "SaleDocuments"
                    FOR EACH ROW EXECUTE FUNCTION guard_sale_document();
                CREATE OR REPLACE FUNCTION guard_sale_settlement() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE
                    payment jsonb;
                    received numeric;
                    financing numeric;
                    method integer;
                BEGIN
                    IF TG_OP <> 'UPDATE' THEN
                        RETURN NEW;
                    END IF;
                    IF OLD."Status" = 'Delivered' THEN
                        RAISE EXCEPTION 'Delivered sales are final'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_SalesOrders_DeliveredImmutable';
                    END IF;
                    IF OLD."Status" = 'Settled' THEN
                        IF NEW."Status" NOT IN ('Settled', 'Delivered') OR (to_jsonb(NEW) - ARRAY['AgentId', 'ClaimedAt', 'UpdatedAt', 'Version', 'Status', 'HandoverEvidence', 'DeliveredAt', 'ClosedAt']) IS DISTINCT FROM
                           (to_jsonb(OLD) - ARRAY['AgentId', 'ClaimedAt', 'UpdatedAt', 'Version', 'Status', 'HandoverEvidence', 'DeliveredAt', 'ClosedAt']) THEN
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
                CREATE OR REPLACE FUNCTION guard_sales_order() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE
                    held "Reservations"%ROWTYPE;
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        RAISE EXCEPTION 'Sales are retained'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_SalesOrders_Immutable';
                    END IF;
                    IF TG_OP = 'INSERT' THEN
                        SELECT * INTO held FROM "Reservations" WHERE "Id" = NEW."Id";
                        IF held."Id" IS NULL OR NEW."Status" <> 'Open' OR NEW."Version" <> 1 OR
                           NEW."DueAt" IS DISTINCT FROM held."ExpiresAt" OR NEW."CreatedAt" <> held."CreatedAt" OR
                           ROW(NEW."BasePrice", NEW."RoadTax", NEW."ProcessingFee", NEW."SstRate", NEW."PromoId",
                               NEW."PromoDiscount", NEW."DepositCredit", NEW."NegotiatedDiscount", NEW."WarrantyPrice",
                               NEW."TradeInAllowance") IS DISTINCT FROM
                           ROW(held."VehiclePrice", held."RoadTax", held."ProcessingFee", held."SstRate", held."PromoId",
                               held."PromoDiscount", held."Deposit", 0::numeric, 0::numeric, 0::numeric) THEN
                            RAISE EXCEPTION 'A sale opens with its reservation''s confirmed terms and deadline'
                                USING ERRCODE = '23514', CONSTRAINT = 'CK_SalesOrders_Origin';
                        END IF;
                        RETURN NEW;
                    END IF;
                    IF OLD."Status" IN ('Expired', 'Cancelled') THEN
                        RAISE EXCEPTION 'A closed sale is final'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_SalesOrders_Immutable';
                    END IF;
                    IF ROW(NEW."Id", NEW."VehicleId", NEW."BranchId", NEW."CustomerId", NEW."CreatedAt", NEW."BasePrice",
                           NEW."RoadTax", NEW."PromoId", NEW."PromoCode", NEW."PromoDiscount", NEW."DepositCredit")
                       IS DISTINCT FROM
                       ROW(OLD."Id", OLD."VehicleId", OLD."BranchId", OLD."CustomerId", OLD."CreatedAt", OLD."BasePrice",
                           OLD."RoadTax", OLD."PromoId", OLD."PromoCode", OLD."PromoDiscount", OLD."DepositCredit") OR
                       NEW."Version" <> OLD."Version" + 1 OR NEW."UpdatedAt" < OLD."UpdatedAt" OR
                       (OLD."OrderNumber" IS NOT NULL AND NEW."OrderNumber" IS DISTINCT FROM OLD."OrderNumber") THEN
                        RAISE EXCEPTION 'A sale keeps its origin and number and advances one version at a time'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_SalesOrders_Immutable';
                    END IF;
                    IF NEW."AgentId" IS NOT NULL AND NEW."AgentId" IS DISTINCT FROM OLD."AgentId" THEN
                        PERFORM 1 FROM "AspNetUsers" WHERE "Id" = NEW."AgentId" AND "IsActive" AND
                            "Role" = 'SalesAgent' AND "BranchId" = NEW."BranchId" FOR SHARE;
                        IF NOT FOUND THEN
                            RAISE EXCEPTION 'A sale is handled by an active sales agent of its branch'
                                USING ERRCODE = '23514', CONSTRAINT = 'CK_SalesOrders_Agent';
                        END IF;
                        PERFORM 1 FROM "Branches" WHERE "Id" = NEW."BranchId" AND "IsActive" FOR SHARE;
                        IF NOT FOUND THEN
                            RAISE EXCEPTION 'A sale is handled only in an active branch'
                                USING ERRCODE = '23514', CONSTRAINT = 'CK_SalesOrders_Agent';
                        END IF;
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE OR REPLACE FUNCTION require_sale_handover() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF NEW."Status" = 'Delivered' AND OLD."Status" <> 'Delivered' AND NOT EXISTS (
                        SELECT 1 FROM "SalesOrderEvents" e JOIN "SaleDocuments" d
                            ON d."IssuingOperationId" = e."Id" AND d."SalesOrderId" = e."SalesOrderId"
                            JOIN "SaleDeliveryMailOutbox" m ON m."DeliveryOperationId" = e."Id" AND m."SalesOrderId" = e."SalesOrderId"
                        WHERE e."SalesOrderId" = NEW."Id" AND e."Sequence" = NEW."Version" AND
                            e."Kind" = 'Delivered' AND e."FromStatus" = 'Settled' AND e."ToStatus" = 'Delivered' AND
                            e."ActorId" = NEW."AgentId" AND e."RecordedAt" = NEW."DeliveredAt" AND
                            e."Snapshot" = to_jsonb(NEW) - ARRAY['Id', 'VehicleId', 'BranchId', 'CustomerId',
                                'PromoId', 'CreatedAt', 'UpdatedAt'] AND d."Kind" = 'HandoverNote' AND
                            d."IssuedAt" = NEW."DeliveredAt") THEN
                        RAISE EXCEPTION 'Delivery requires its exact history, handover note and durable mail intent'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_SalesOrders_HandoverReceipt';
                    END IF;
                    RETURN NULL;
                END $$;
                CREATE OR REPLACE FUNCTION guard_sale_document_correction() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE
                    actor "AspNetUsers"%ROWTYPE;
                    sale "SalesOrders"%ROWTYPE;
                    original "SaleDocuments"%ROWTYPE;
                BEGIN
                    IF TG_OP <> 'INSERT' THEN
                        RAISE EXCEPTION 'Document correction evidence is append-only'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_SaleDocumentCorrections_Immutable';
                    END IF;
                    SELECT * INTO actor FROM "AspNetUsers" WHERE "Id" = NEW."ActorId" FOR SHARE;
                    SELECT * INTO sale FROM "SalesOrders" WHERE "Id" = NEW."SalesOrderId" FOR UPDATE;
                    SELECT * INTO original FROM "SaleDocuments" WHERE "Id" = NEW."OriginalDocumentId";
                    PERFORM 1 FROM "Branches" WHERE "Id" = sale."BranchId" AND "IsActive" FOR SHARE;
                    IF NOT FOUND OR NOT COALESCE(actor."IsActive" AND
                        (actor."Role" = 'GeneralManager' OR
                            (actor."Role" = 'BranchManager' AND actor."BranchId" = sale."BranchId")) AND
                        sale."Status" IN ('Settled', 'Delivered') AND NEW."ExpectedVersion" = sale."Version" AND
                        original."SalesOrderId" = sale."Id" AND NEW."RecordedAt" >= original."IssuedAt" AND
                        NEW."RecordedAt" <= clock_timestamp() AND
                        (NEW."BuyerName" IS DISTINCT FROM original."ContentsJson"->'Review'->>'BuyerName' OR
                         NEW."BuyerAddress" IS DISTINCT FROM original."ContentsJson"->'Review'->>'BuyerAddress') AND
                        NOT EXISTS (SELECT 1 FROM "SalesOrderEvents" WHERE "Id" = NEW."Id"), false) THEN
                        RAISE EXCEPTION 'Correction requires current manager scope, version and changed buyer spelling'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_SaleDocumentCorrections_Scope';
                    END IF;
                    RETURN NEW;
                END $$;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_SalesOrders_AspNetUsers_RetentionHoldById",
                table: "SalesOrders");

            migrationBuilder.DropIndex(
                name: "IX_SalesOrders_RetentionHoldById",
                table: "SalesOrders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SalesOrders_Hold",
                table: "SalesOrders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SaleDocumentCorrections_Shape",
                table: "SaleDocumentCorrections");

            migrationBuilder.DropColumn(
                name: "RetentionHoldById",
                table: "SalesOrders");

            migrationBuilder.DropColumn(
                name: "RetentionHoldReason",
                table: "SalesOrders");

            migrationBuilder.DropColumn(
                name: "RetentionHoldUntil",
                table: "SalesOrders");

            migrationBuilder.DropColumn(
                name: "MinimizedAt",
                table: "SaleDocumentCorrections");

            migrationBuilder.AlterColumn<string>(
                name: "Reason",
                table: "SaleDocumentCorrections",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "BuyerName",
                table: "SaleDocumentCorrections",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "BuyerAddress",
                table: "SaleDocumentCorrections",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_SaleDocumentCorrections_Shape",
                table: "SaleDocumentCorrections",
                sql: "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"ExpectedVersion\" > 0 AND isfinite(\"RecordedAt\") AND length(\"BuyerName\") BETWEEN 1 AND 200 AND length(\"BuyerAddress\") BETWEEN 1 AND 1000 AND length(\"Reason\") BETWEEN 1 AND 1000 AND \"BuyerName\" = btrim(\"BuyerName\") AND \"BuyerName\" ~ '[^[:space:]]' AND \"BuyerName\" !~ '[[:cntrl:]]' AND \"BuyerAddress\" = btrim(\"BuyerAddress\") AND \"BuyerAddress\" ~ '[^[:space:]]' AND \"BuyerAddress\" !~ '[[:cntrl:]]' AND \"Reason\" = btrim(\"Reason\") AND \"Reason\" ~ '[^[:space:]]' AND \"Reason\" !~ '[[:cntrl:]]'");
        }
    }
}

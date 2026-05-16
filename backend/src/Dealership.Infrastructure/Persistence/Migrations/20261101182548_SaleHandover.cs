using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dealership.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SaleHandover : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SaleDocuments_SalesOrderEvents_SettlementOperationId_SalesO~",
                table: "SaleDocuments");

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
                name: "CK_SalesOrders_Settlement",
                table: "SalesOrders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SalesOrders_Shape",
                table: "SalesOrders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SalesOrderEvents_Shape",
                table: "SalesOrderEvents");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SaleDocuments_Shape",
                table: "SaleDocuments");

            migrationBuilder.RenameColumn(
                name: "SettlementOperationId",
                table: "SaleDocuments",
                newName: "IssuingOperationId");

            migrationBuilder.RenameIndex(
                name: "IX_SaleDocuments_SettlementOperationId_SalesOrderId",
                table: "SaleDocuments",
                newName: "IX_SaleDocuments_IssuingOperationId_SalesOrderId");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeliveredAt",
                table: "SalesOrders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HandoverEvidence",
                table: "SalesOrders",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "HandoverNumberCounters",
                columns: table => new
                {
                    Year = table.Column<int>(type: "integer", nullable: false),
                    LastValue = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HandoverNumberCounters", x => x.Year);
                    table.CheckConstraint("CK_HandoverNumberCounters_Shape", "\"Year\" BETWEEN 2000 AND 9999 AND \"LastValue\" BETWEEN 0 AND 999999");
                });

            migrationBuilder.CreateTable(
                name: "SaleDeliveryMailOutbox",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SalesOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeliveryOperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    DeliveryAttempts = table.Column<int>(type: "integer", nullable: false),
                    NextDeliveryAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeliveryClaimId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeliveryClaimExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeliveredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaleDeliveryMailOutbox", x => x.Id);
                    table.CheckConstraint("CK_SaleDeliveryMailOutbox_Shape", "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"DeliveryAttempts\" >= 0 AND \"RecipientEmail\" ~ '[^[:space:]]' AND isfinite(\"NextDeliveryAt\") AND ((\"DeliveryClaimId\" IS NULL) = (\"DeliveryClaimExpiresAt\" IS NULL))");
                    table.ForeignKey(
                        name: "FK_SaleDeliveryMailOutbox_SalesOrderEvents_DeliveryOperationId~",
                        columns: x => new { x.DeliveryOperationId, x.SalesOrderId },
                        principalTable: "SalesOrderEvents",
                        principalColumns: ["Id", "SalesOrderId"],
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SaleDeliveryMailOutbox_SalesOrders_SalesOrderId",
                        column: x => x.SalesOrderId,
                        principalTable: "SalesOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Vehicles_State",
                table: "Vehicles",
                sql: "\"MileageKilometres\" >= 0 AND \"Status\" IN ('PendingInspection', 'Rejected', 'Available', 'Sold', 'Delivered') AND \"ManufactureYear\" > 0");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_TradeInAppointmentId",
                table: "SalesOrders",
                column: "TradeInAppointmentId",
                unique: true,
                filter: "\"TradeInAppointmentId\" IS NOT NULL AND \"Status\" IN ('Open', 'Handling', 'PendingApproval', 'Approved', 'Settled', 'Delivered')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SalesOrders_Approval",
                table: "SalesOrders",
                sql: "(\"Status\" = 'PendingApproval' AND \"DiscountLimitAtSubmit\" IS NOT NULL AND \"DecidedById\" IS NULL AND \"NegotiatedDiscount\" > \"DiscountLimitAtSubmit\") OR (\"Status\" IN ('Approved', 'Settled', 'Delivered') AND \"DiscountLimitAtSubmit\" IS NOT NULL AND \"DecidedAt\" IS NOT NULL AND (\"DecidedById\" IS NOT NULL OR \"NegotiatedDiscount\" <= \"DiscountLimitAtSubmit\") AND \"DecidedById\" IS DISTINCT FROM \"AgentId\") OR (\"Status\" IN ('Open', 'Handling') AND \"DiscountLimitAtSubmit\" IS NULL AND \"DecidedById\" IS NULL) OR \"Status\" IN ('Expired', 'Cancelled')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SalesOrders_Delivery",
                table: "SalesOrders",
                sql: "(\"Status\" = 'Delivered' AND \"HandoverEvidence\" IS NOT NULL AND \"DeliveredAt\" IS NOT NULL AND isfinite(\"DeliveredAt\") AND \"DeliveredAt\" >= \"SettledAt\" AND \"DeliveredAt\" = \"UpdatedAt\") OR (\"Status\" <> 'Delivered' AND \"HandoverEvidence\" IS NULL AND \"DeliveredAt\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SalesOrders_Handling",
                table: "SalesOrders",
                sql: "(\"Status\" = 'Open' AND \"DueAt\" IS NOT NULL AND \"AgentId\" IS NULL AND \"ClaimedAt\" IS NULL AND \"PausedSeconds\" IS NULL AND \"ClosedAt\" IS NULL) OR (\"Status\" IN ('Handling', 'PendingApproval', 'Approved') AND \"DueAt\" IS NULL AND \"AgentId\" IS NOT NULL AND \"ClaimedAt\" IS NOT NULL AND \"PausedSeconds\" BETWEEN 1 AND 172800 AND \"ClosedAt\" IS NULL) OR (\"Status\" IN ('Expired', 'Cancelled') AND \"AgentId\" IS NULL AND \"ClaimedAt\" IS NULL AND \"PausedSeconds\" IS NULL AND \"ClosedAt\" IS NOT NULL) OR (\"Status\" = 'Settled' AND \"DueAt\" IS NULL AND \"AgentId\" IS NOT NULL AND \"ClaimedAt\" IS NOT NULL AND \"PausedSeconds\" IS NULL AND \"ClosedAt\" IS NULL) OR (\"Status\" = 'Delivered' AND \"DueAt\" IS NULL AND \"AgentId\" IS NOT NULL AND \"ClaimedAt\" IS NOT NULL AND \"PausedSeconds\" IS NULL AND \"ClosedAt\" = \"DeliveredAt\")");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SalesOrders_Settlement",
                table: "SalesOrders",
                sql: "(\"Status\" IN ('Settled', 'Delivered') AND \"PaymentEvidence\" IS NOT NULL AND \"SettledAt\" IS NOT NULL AND isfinite(\"SettledAt\") AND \"SettledAt\" >= \"DecidedAt\" AND \"SettledAt\" <= \"UpdatedAt\" AND (\"TradeInAppointmentId\" IS NULL) = (\"AcquiredVehicleId\" IS NULL) AND \"AcquiredVehicleId\" IS DISTINCT FROM \"VehicleId\") OR (\"Status\" NOT IN ('Settled', 'Delivered') AND \"PaymentEvidence\" IS NULL AND \"SettledAt\" IS NULL AND \"AcquiredVehicleId\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SalesOrders_Shape",
                table: "SalesOrders",
                sql: "\"Version\" > 0 AND \"Status\" IN ('Open', 'Handling', 'PendingApproval', 'Approved', 'Expired', 'Cancelled', 'Settled', 'Delivered') AND isfinite(\"CreatedAt\") AND isfinite(\"UpdatedAt\") AND \"UpdatedAt\" >= \"CreatedAt\" AND ((\"OrderNumber\" IS NULL AND \"SubmittedAt\" IS NULL) OR (\"OrderNumber\" ~ '^VPO-[0-9]{4}-[0-9]{6}$' AND \"SubmittedAt\" IS NOT NULL))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SalesOrderEvents_Shape",
                table: "SalesOrderEvents",
                sql: "isfinite(\"RecordedAt\") AND \"Sequence\" > 0 AND \"Kind\" IN ('Created', 'Claimed', 'Assigned', 'Reassigned', 'Released', 'TermsSaved', 'Submitted', 'Approved', 'Rejected', 'Cancelled', 'CustomerCancelled', 'Expired', 'Withdrawn', 'Settled', 'Delivered') AND \"ToStatus\" IN ('Open', 'Handling', 'PendingApproval', 'Approved', 'Expired', 'Cancelled', 'Settled', 'Delivered') AND (\"FromStatus\" IS NULL OR \"FromStatus\" IN ('Open', 'Handling', 'PendingApproval', 'Approved', 'Expired', 'Cancelled', 'Settled', 'Delivered')) AND (\"FromStatus\" IS NULL) = (\"Kind\" = 'Created') AND (\"ActorId\" IS NULL) = (\"Kind\" IN ('Expired', 'Withdrawn')) AND ((\"Kind\" IN ('Rejected', 'Cancelled', 'Withdrawn') AND \"Reason\" IS NOT NULL AND \"Reason\" = btrim(\"Reason\") AND length(\"Reason\") BETWEEN 1 AND 1000 AND \"Reason\" !~ '[[:cntrl:]]') OR (\"Kind\" NOT IN ('Rejected', 'Cancelled', 'Withdrawn') AND \"Reason\" IS NULL)) AND (\"ReleaseId\" IS NOT NULL) = (\"Kind\" IN ('Cancelled', 'CustomerCancelled', 'Expired', 'Withdrawn')) AND (\"Changes\" IS NULL OR (\"Kind\" IN ('TermsSaved', 'Released') AND jsonb_typeof(\"Changes\") = 'array')) AND jsonb_typeof(\"Snapshot\") = 'object' AND \"Snapshot\" ->> 'Status' = \"ToStatus\" AND (\"Snapshot\" ->> 'Version')::bigint = \"Sequence\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SaleDocuments_Shape",
                table: "SaleDocuments",
                sql: "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND isfinite(\"IssuedAt\") AND ((\"Kind\" = 'PurchaseOrder' AND \"Number\" ~ '^VPO-[0-9]{4}-[0-9]{6}$') OR (\"Kind\" = 'PaymentReceipt' AND \"Number\" ~ '^VRC-[0-9]{4}-[0-9]{6}$') OR (\"Kind\" = 'HandoverNote' AND \"Number\" ~ '^VHN-[0-9]{4}-[0-9]{6}$')) AND jsonb_typeof(\"ContentsJson\") = 'object' AND octet_length(\"ContentsJson\"::text) <= 65536");

            migrationBuilder.CreateIndex(
                name: "IX_SaleDeliveryMailOutbox_DeliveryOperationId_SalesOrderId",
                table: "SaleDeliveryMailOutbox",
                columns: ["DeliveryOperationId", "SalesOrderId"]);

            migrationBuilder.CreateIndex(
                name: "IX_SaleDeliveryMailOutbox_NextDeliveryAt_Id",
                table: "SaleDeliveryMailOutbox",
                columns: ["NextDeliveryAt", "Id"]);

            migrationBuilder.CreateIndex(
                name: "IX_SaleDeliveryMailOutbox_SalesOrderId",
                table: "SaleDeliveryMailOutbox",
                column: "SalesOrderId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_SaleDocuments_SalesOrderEvents_IssuingOperationId_SalesOrde~",
                table: "SaleDocuments",
                columns: ["IssuingOperationId", "SalesOrderId"],
                principalTable: "SalesOrderEvents",
                principalColumns: ["Id", "SalesOrderId"],
                onDelete: ReferentialAction.Restrict);
            migrationBuilder.Sql("""
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
                CREATE OR REPLACE FUNCTION inspection_vehicle_status(vehicle_id uuid) RETURNS text LANGUAGE plpgsql AS $$
                DECLARE
                    draft "InspectionDrafts"%ROWTYPE;
                    report jsonb;
                    request "InspectionRepairRequests"%ROWTYPE;
                BEGIN
                    IF EXISTS (SELECT 1 FROM "SalesOrders" WHERE "VehicleId" = vehicle_id AND "Status" = 'Delivered') THEN
                        RETURN 'Delivered';
                    END IF;
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
                CREATE OR REPLACE FUNCTION require_reservation_release() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "Reservations" r WHERE r."Id" = NEW."Id" AND r."IsActive" =
                        (EXISTS (SELECT 1 FROM "ReservationReleases" WHERE "ReservationId" = r."Id") OR
                         EXISTS (SELECT 1 FROM "SalesOrders" WHERE "Id" = r."Id" AND "Status" IN ('Settled', 'Delivered')))) THEN
                        RAISE EXCEPTION 'Reservation slots must agree with release or settlement evidence'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_Reservations_Release';
                    END IF;
                    RETURN NULL;
                END $$;
                CREATE OR REPLACE FUNCTION guard_sale_document() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE
                    sale "SalesOrders"%ROWTYPE;
                    receipt "SalesOrderEvents"%ROWTYPE;
                    branch "Branches"%ROWTYPE;
                    reservation_number text;
                BEGIN
                    IF TG_OP <> 'INSERT' THEN
                        RAISE EXCEPTION 'Issued sales documents are immutable'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_SaleDocuments_Immutable';
                    END IF;
                    SELECT * INTO sale FROM "SalesOrders" WHERE "Id" = NEW."SalesOrderId" FOR UPDATE;
                    SELECT * INTO receipt FROM "SalesOrderEvents" WHERE "Id" = NEW."IssuingOperationId";
                    IF NEW."Kind" = 'HandoverNote' THEN
                        IF NOT COALESCE(receipt."Kind" = 'Delivered' AND receipt."SalesOrderId" = sale."Id" AND
                            sale."Status" = 'Delivered' AND receipt."Sequence" = sale."Version" AND
                            NEW."IssuedAt" = receipt."RecordedAt" AND
                            NEW."ContentsJson"->'Sale' = receipt."Snapshot" AND
                            NEW."ContentsJson" - 'Sale' = (SELECT "ContentsJson" - 'Sale' FROM "SaleDocuments"
                                WHERE "SalesOrderId" = sale."Id" AND "Kind" = 'PurchaseOrder'), false) THEN
                            RAISE EXCEPTION 'Handover notes retain settlement identity and exact delivery evidence'
                                USING ERRCODE = '23514', CONSTRAINT = 'CK_SaleDocuments_Handover';
                        END IF;
                        RETURN NEW;
                    END IF;
                    SELECT * INTO branch FROM "Branches" WHERE "Id" = sale."BranchId" FOR SHARE;
                    SELECT "Number" INTO reservation_number FROM "Reservations" WHERE "Id" = sale."Id";
                    IF NOT COALESCE(receipt."Kind" = 'Settled' AND receipt."SalesOrderId" = sale."Id" AND
                        sale."Status" = 'Settled' AND receipt."Sequence" = sale."Version" AND
                        NEW."IssuedAt" = receipt."RecordedAt" AND receipt."SettlementReview" IS NOT NULL AND
                        (NEW."Kind" <> 'PurchaseOrder' OR NEW."Number" = sale."OrderNumber") AND
                        NEW."ContentsJson" = jsonb_build_object('CustomerId', sale."CustomerId",
                            'BranchId', sale."BranchId", 'BranchName', branch."Name", 'BranchAddress', branch."Address",
                            'ReservationNumber', reservation_number, 'Review', receipt."SettlementReview",
                            'Sale', receipt."Snapshot"), false) THEN
                        RAISE EXCEPTION 'Documents must retain the exact settlement and reviewed facts'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_SaleDocuments_Settlement';
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE OR REPLACE FUNCTION require_settlement_documents() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF NEW."Status" = 'Settled' AND OLD."Status" <> 'Settled' AND
                        (SELECT count(*) FROM "SaleDocuments" d JOIN "SalesOrderEvents" e
                            ON e."Id" = d."IssuingOperationId" AND e."SalesOrderId" = d."SalesOrderId"
                            WHERE d."SalesOrderId" = NEW."Id" AND e."Kind" = 'Settled' AND
                                e."Sequence" = NEW."Version" AND d."IssuedAt" = NEW."SettledAt") <> 2 THEN
                        RAISE EXCEPTION 'Settlement requires both issued documents in the same transaction'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_SalesOrders_Documents';
                    END IF;
                    RETURN NULL;
                END $$;
                CREATE FUNCTION guard_sale_handover() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE
                    vehicle "Vehicles"%ROWTYPE;
                    evidence jsonb;
                    odometer numeric;
                    keys numeric;
                BEGIN
                    IF NEW."Status" <> 'Delivered' OR OLD."Status" = 'Delivered' THEN
                        RETURN NEW;
                    END IF;
                    IF OLD."Status" <> 'Settled' OR NEW."DeliveredAt" <> NEW."UpdatedAt" OR
                        NEW."DeliveredAt" > clock_timestamp() OR NEW."ClosedAt" <> NEW."DeliveredAt" OR
                        (to_jsonb(NEW) - ARRAY['Status', 'HandoverEvidence', 'DeliveredAt', 'ClosedAt', 'UpdatedAt', 'Version'])
                        IS DISTINCT FROM
                        (to_jsonb(OLD) - ARRAY['Status', 'HandoverEvidence', 'DeliveredAt', 'ClosedAt', 'UpdatedAt', 'Version']) THEN
                        RAISE EXCEPTION 'Only unchanged settled sales may be handed over'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_SalesOrders_HandoverTransition';
                    END IF;
                    PERFORM 1 FROM "AspNetUsers" WHERE "Id" = NEW."AgentId" AND "IsActive" AND
                        "Role" = 'SalesAgent' AND "BranchId" = NEW."BranchId" FOR SHARE;
                    IF NOT FOUND THEN
                        RAISE EXCEPTION 'Handover requires the current eligible handling agent'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_SalesOrders_HandoverActor';
                    END IF;
                    PERFORM 1 FROM "Branches" WHERE "Id" = NEW."BranchId" AND "IsActive" FOR SHARE;
                    IF NOT FOUND THEN
                        RAISE EXCEPTION 'Handover requires an active branch'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_SalesOrders_HandoverActor';
                    END IF;
                    SELECT * INTO vehicle FROM "Vehicles" WHERE "Id" = NEW."VehicleId" FOR UPDATE;
                    evidence := NEW."HandoverEvidence";
                    odometer := (evidence->>'OdometerKilometres')::numeric;
                    keys := (evidence->>'KeysDelivered')::numeric;
                    IF NOT COALESCE(vehicle."Status" = 'Sold' AND jsonb_typeof(evidence) = 'object' AND
                        jsonb_typeof(evidence->'OdometerKilometres') = 'number' AND
                        odometer BETWEEN vehicle."MileageKilometres" AND 2147483647 AND odometer = trunc(odometer) AND
                        jsonb_typeof(evidence->'KeysDelivered') = 'number' AND
                        keys BETWEEN 0 AND 2147483647 AND keys = trunc(keys) AND
                        evidence->'CustomerAcknowledged' = 'true'::jsonb AND
                        evidence->'OwnershipTransferCompleted' = 'true'::jsonb AND
                        evidence->'InsuranceConfirmed' = 'true'::jsonb, false) THEN
                        RAISE EXCEPTION 'Handover requires a nondecreasing odometer, keys and all confirmations'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_SalesOrders_HandoverEvidence';
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER sale_handover_guard BEFORE UPDATE ON "SalesOrders"
                    FOR EACH ROW EXECUTE FUNCTION guard_sale_handover();

                CREATE FUNCTION apply_sale_handover() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF NEW."Status" = 'Delivered' AND OLD."Status" <> 'Delivered' THEN
                        UPDATE "Vehicles" SET "Status" = 'Delivered',
                            "MileageKilometres" = (NEW."HandoverEvidence"->>'OdometerKilometres')::integer
                            WHERE "Id" = NEW."VehicleId";
                    END IF;
                    RETURN NULL;
                END $$;
                CREATE TRIGGER sale_delivered AFTER UPDATE ON "SalesOrders"
                    FOR EACH ROW EXECUTE FUNCTION apply_sale_handover();

                CREATE FUNCTION require_sale_handover() RETURNS trigger LANGUAGE plpgsql AS $$
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
                CREATE CONSTRAINT TRIGGER sale_handover_required AFTER UPDATE ON "SalesOrders"
                    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION require_sale_handover();

                CREATE FUNCTION guard_sale_delivery_mail() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP = 'DELETE' OR (TG_OP = 'UPDATE' AND
                        ROW(NEW."Id", NEW."SalesOrderId", NEW."DeliveryOperationId", NEW."RecipientEmail") IS DISTINCT FROM
                        ROW(OLD."Id", OLD."SalesOrderId", OLD."DeliveryOperationId", OLD."RecipientEmail")) THEN
                        RAISE EXCEPTION 'Delivery mail retains its original identity and recipient'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_SaleDeliveryMailOutbox_Immutable';
                    END IF;
                    IF TG_OP = 'INSERT' AND NOT EXISTS (
                        SELECT 1 FROM "SalesOrderEvents" e JOIN "Reservations" r ON r."Id" = e."SalesOrderId"
                        WHERE e."Id" = NEW."DeliveryOperationId" AND e."SalesOrderId" = NEW."SalesOrderId" AND
                            e."Kind" = 'Delivered' AND NEW."RecipientEmail" = r."BuyerEmail" AND
                            NEW."NextDeliveryAt" = e."RecordedAt" AND NEW."DeliveryAttempts" = 0 AND
                            NEW."DeliveredAt" IS NULL AND NEW."DeliveryClaimId" IS NULL) THEN
                        RAISE EXCEPTION 'Delivery mail requires its delivery receipt and original customer recipient'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_SaleDeliveryMailOutbox_Delivery';
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER sale_delivery_mail_guard BEFORE INSERT OR UPDATE OR DELETE ON "SaleDeliveryMailOutbox"
                    FOR EACH ROW EXECUTE FUNCTION guard_sale_delivery_mail();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "SalesOrders" WHERE "Status" = 'Delivered') OR
                        EXISTS (SELECT 1 FROM "SaleDeliveryMailOutbox") OR
                        EXISTS (SELECT 1 FROM "SaleDocuments" WHERE "Kind" = 'HandoverNote') THEN
                        RAISE EXCEPTION 'Cannot remove retained handover evidence';
                    END IF;
                END $$;
                DROP TRIGGER sale_handover_guard ON "SalesOrders";
                DROP FUNCTION guard_sale_handover();
                DROP TRIGGER sale_delivered ON "SalesOrders";
                DROP FUNCTION apply_sale_handover();
                DROP TRIGGER sale_handover_required ON "SalesOrders";
                DROP FUNCTION require_sale_handover();
                DROP TRIGGER sale_delivery_mail_guard ON "SaleDeliveryMailOutbox";
                DROP FUNCTION guard_sale_delivery_mail();
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_SaleDocuments_SalesOrderEvents_IssuingOperationId_SalesOrde~",
                table: "SaleDocuments");

            migrationBuilder.DropTable(
                name: "HandoverNumberCounters");

            migrationBuilder.DropTable(
                name: "SaleDeliveryMailOutbox");

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
                name: "CK_SalesOrders_Delivery",
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

            migrationBuilder.DropCheckConstraint(
                name: "CK_SaleDocuments_Shape",
                table: "SaleDocuments");

            migrationBuilder.DropColumn(
                name: "DeliveredAt",
                table: "SalesOrders");

            migrationBuilder.DropColumn(
                name: "HandoverEvidence",
                table: "SalesOrders");

            migrationBuilder.RenameColumn(
                name: "IssuingOperationId",
                table: "SaleDocuments",
                newName: "SettlementOperationId");

            migrationBuilder.RenameIndex(
                name: "IX_SaleDocuments_IssuingOperationId_SalesOrderId",
                table: "SaleDocuments",
                newName: "IX_SaleDocuments_SettlementOperationId_SalesOrderId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Vehicles_State",
                table: "Vehicles",
                sql: "\"MileageKilometres\" >= 0 AND \"Status\" IN ('PendingInspection', 'Rejected', 'Available', 'Sold') AND \"ManufactureYear\" > 0");

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

            migrationBuilder.AddCheckConstraint(
                name: "CK_SaleDocuments_Shape",
                table: "SaleDocuments",
                sql: "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND isfinite(\"IssuedAt\") AND ((\"Kind\" = 'PurchaseOrder' AND \"Number\" ~ '^VPO-[0-9]{4}-[0-9]{6}$') OR (\"Kind\" = 'PaymentReceipt' AND \"Number\" ~ '^VRC-[0-9]{4}-[0-9]{6}$')) AND jsonb_typeof(\"ContentsJson\") = 'object' AND octet_length(\"ContentsJson\"::text) <= 65536");

            migrationBuilder.AddForeignKey(
                name: "FK_SaleDocuments_SalesOrderEvents_SettlementOperationId_SalesO~",
                table: "SaleDocuments",
                columns: ["SettlementOperationId", "SalesOrderId"],
                principalTable: "SalesOrderEvents",
                principalColumns: ["Id", "SalesOrderId"],
                onDelete: ReferentialAction.Restrict);
            migrationBuilder.Sql("""
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
                CREATE OR REPLACE FUNCTION guard_sale_document() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE
                    sale "SalesOrders"%ROWTYPE;
                    receipt "SalesOrderEvents"%ROWTYPE;
                    branch "Branches"%ROWTYPE;
                    reservation_number text;
                BEGIN
                    IF TG_OP <> 'INSERT' THEN
                        RAISE EXCEPTION 'Issued sales documents are immutable'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_SaleDocuments_Immutable';
                    END IF;
                    SELECT * INTO sale FROM "SalesOrders" WHERE "Id" = NEW."SalesOrderId" FOR UPDATE;
                    SELECT * INTO receipt FROM "SalesOrderEvents" WHERE "Id" = NEW."SettlementOperationId";
                    SELECT * INTO branch FROM "Branches" WHERE "Id" = sale."BranchId" FOR SHARE;
                    SELECT "Number" INTO reservation_number FROM "Reservations" WHERE "Id" = sale."Id";
                    IF NOT COALESCE(receipt."Kind" = 'Settled' AND receipt."SalesOrderId" = sale."Id" AND
                        sale."Status" = 'Settled' AND receipt."Sequence" = sale."Version" AND
                        NEW."IssuedAt" = receipt."RecordedAt" AND receipt."SettlementReview" IS NOT NULL AND
                        (NEW."Kind" <> 'PurchaseOrder' OR NEW."Number" = sale."OrderNumber") AND
                        NEW."ContentsJson" = jsonb_build_object('CustomerId', sale."CustomerId",
                            'BranchId', sale."BranchId", 'BranchName', branch."Name", 'BranchAddress', branch."Address",
                            'ReservationNumber', reservation_number, 'Review', receipt."SettlementReview",
                            'Sale', receipt."Snapshot"), false) THEN
                        RAISE EXCEPTION 'Documents must retain the exact settlement and reviewed facts'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_SaleDocuments_Settlement';
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE OR REPLACE FUNCTION require_settlement_documents() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF NEW."Status" = 'Settled' AND OLD."Status" <> 'Settled' AND
                        (SELECT count(*) FROM "SaleDocuments" d JOIN "SalesOrderEvents" e
                            ON e."Id" = d."SettlementOperationId" AND e."SalesOrderId" = d."SalesOrderId"
                            WHERE d."SalesOrderId" = NEW."Id" AND e."Kind" = 'Settled' AND
                                e."Sequence" = NEW."Version" AND d."IssuedAt" = NEW."SettledAt") <> 2 THEN
                        RAISE EXCEPTION 'Settlement requires both issued documents in the same transaction'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_SalesOrders_Documents';
                    END IF;
                    RETURN NULL;
                END $$;
                """);
        }
    }
}

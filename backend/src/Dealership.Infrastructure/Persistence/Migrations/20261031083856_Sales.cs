using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dealership.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ReservationReleases_Actor",
                table: "ReservationReleases");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ReservationReleases_Shape",
                table: "ReservationReleases");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Reservations_Id_VehicleId_BranchId_CustomerId",
                table: "Reservations",
                columns: new[] { "Id", "VehicleId", "BranchId", "CustomerId" });

            migrationBuilder.CreateTable(
                name: "DealNumberCounters",
                columns: table => new
                {
                    Year = table.Column<int>(type: "integer", nullable: false),
                    LastValue = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DealNumberCounters", x => x.Year);
                    table.CheckConstraint("CK_DealNumberCounters_Shape", "\"Year\" BETWEEN 2000 AND 9999 AND \"LastValue\" BETWEEN 0 AND 999999");
                });

            migrationBuilder.CreateTable(
                name: "SalesOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AgentId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClaimedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DueAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PausedSeconds = table.Column<long>(type: "bigint", nullable: true),
                    OrderNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true, collation: "C"),
                    PromoId = table.Column<Guid>(type: "uuid", nullable: true),
                    PromoCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true, collation: "C"),
                    BasePrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RoadTax = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ProcessingFee = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    SstRate = table.Column<decimal>(type: "numeric(29,28)", precision: 29, scale: 28, nullable: false),
                    ServiceTax = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PromoDiscount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    NegotiatedDiscount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    WarrantyPackageId = table.Column<Guid>(type: "uuid", nullable: true),
                    WarrantyName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    WarrantyPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DepositCredit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TradeInAppointmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    TradeInLabel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    TradeInAllowance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    BalancePayable = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DiscountLimitAtSubmit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    DecidedById = table.Column<Guid>(type: "uuid", nullable: true),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesOrders", x => x.Id);
                    table.CheckConstraint("CK_SalesOrders_Approval", "(\"Status\" = 'PendingApproval' AND \"DiscountLimitAtSubmit\" IS NOT NULL AND \"DecidedById\" IS NULL AND \"NegotiatedDiscount\" > \"DiscountLimitAtSubmit\") OR (\"Status\" = 'Approved' AND \"DiscountLimitAtSubmit\" IS NOT NULL AND \"DecidedAt\" IS NOT NULL AND (\"DecidedById\" IS NOT NULL OR \"NegotiatedDiscount\" <= \"DiscountLimitAtSubmit\") AND \"DecidedById\" IS DISTINCT FROM \"AgentId\") OR (\"Status\" IN ('Open', 'Handling') AND \"DiscountLimitAtSubmit\" IS NULL AND \"DecidedById\" IS NULL) OR \"Status\" IN ('Expired', 'Cancelled')");
                    table.CheckConstraint("CK_SalesOrders_Handling", "(\"Status\" = 'Open' AND \"DueAt\" IS NOT NULL AND \"AgentId\" IS NULL AND \"ClaimedAt\" IS NULL AND \"PausedSeconds\" IS NULL AND \"ClosedAt\" IS NULL) OR (\"Status\" IN ('Handling', 'PendingApproval', 'Approved') AND \"DueAt\" IS NULL AND \"AgentId\" IS NOT NULL AND \"ClaimedAt\" IS NOT NULL AND \"PausedSeconds\" BETWEEN 1 AND 172800 AND \"ClosedAt\" IS NULL) OR (\"Status\" IN ('Expired', 'Cancelled') AND \"AgentId\" IS NULL AND \"ClaimedAt\" IS NULL AND \"PausedSeconds\" IS NULL AND \"ClosedAt\" IS NOT NULL)");
                    table.CheckConstraint("CK_SalesOrders_Price", "\"BasePrice\" > 0 AND \"RoadTax\" >= 0 AND \"ProcessingFee\" >= 0 AND \"SstRate\" BETWEEN 0 AND 1 AND \"PromoDiscount\" >= 0 AND \"NegotiatedDiscount\" >= 0 AND \"WarrantyPrice\" >= 0 AND \"DepositCredit\" >= 0 AND \"TradeInAllowance\" >= 0 AND \"PromoDiscount\" + \"NegotiatedDiscount\" <= \"BasePrice\" AND \"ServiceTax\" = round(\"ProcessingFee\" * \"SstRate\", 2) AND \"BalancePayable\" >= 0 AND \"BalancePayable\" = \"BasePrice\" - \"PromoDiscount\" - \"NegotiatedDiscount\" + \"ProcessingFee\" + \"RoadTax\" + \"ServiceTax\" + \"WarrantyPrice\" - \"DepositCredit\" - \"TradeInAllowance\"");
                    table.CheckConstraint("CK_SalesOrders_Shape", "\"Version\" > 0 AND \"Status\" IN ('Open', 'Handling', 'PendingApproval', 'Approved', 'Expired', 'Cancelled') AND isfinite(\"CreatedAt\") AND isfinite(\"UpdatedAt\") AND \"UpdatedAt\" >= \"CreatedAt\" AND ((\"OrderNumber\" IS NULL AND \"SubmittedAt\" IS NULL) OR (\"OrderNumber\" ~ '^VPO-[0-9]{4}-[0-9]{6}$' AND \"SubmittedAt\" IS NOT NULL))");
                    table.CheckConstraint("CK_SalesOrders_Terms", "((\"PromoId\" IS NULL AND \"PromoCode\" IS NULL AND \"PromoDiscount\" = 0) OR (\"PromoId\" IS NOT NULL AND \"PromoCode\" IS NOT NULL)) AND ((\"WarrantyPackageId\" IS NULL AND \"WarrantyName\" IS NULL AND \"WarrantyPrice\" = 0) OR (\"WarrantyPackageId\" IS NOT NULL AND \"WarrantyName\" IS NOT NULL)) AND ((\"TradeInAppointmentId\" IS NULL AND \"TradeInLabel\" IS NULL AND \"TradeInAllowance\" = 0) OR (\"TradeInAppointmentId\" IS NOT NULL AND \"TradeInLabel\" IS NOT NULL AND \"TradeInAllowance\" > 0))");
                    table.ForeignKey(
                        name: "FK_SalesOrders_AspNetUsers_AgentId",
                        column: x => x.AgentId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesOrders_AspNetUsers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesOrders_AspNetUsers_DecidedById",
                        column: x => x.DecidedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesOrders_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesOrders_IntakeAppointments_TradeInAppointmentId",
                        column: x => x.TradeInAppointmentId,
                        principalTable: "IntakeAppointments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesOrders_PromoCodes_PromoId",
                        column: x => x.PromoId,
                        principalTable: "PromoCodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesOrders_Reservations_Id_VehicleId_BranchId_CustomerId",
                        columns: x => new { x.Id, x.VehicleId, x.BranchId, x.CustomerId },
                        principalTable: "Reservations",
                        principalColumns: new[] { "Id", "VehicleId", "BranchId", "CustomerId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesOrders_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesOrders_WarrantyPackages_WarrantyPackageId",
                        column: x => x.WarrantyPackageId,
                        principalTable: "WarrantyPackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SalesOrderEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SalesOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    FromStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    ToStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: true),
                    AgentId = table.Column<Guid>(type: "uuid", nullable: true),
                    PreviousAgentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Changes = table.Column<string>(type: "jsonb", nullable: true),
                    Snapshot = table.Column<string>(type: "jsonb", nullable: false),
                    ReleaseId = table.Column<Guid>(type: "uuid", nullable: true),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesOrderEvents", x => x.Id);
                    table.CheckConstraint("CK_SalesOrderEvents_Shape", "isfinite(\"RecordedAt\") AND \"Sequence\" > 0 AND \"Kind\" IN ('Created', 'Claimed', 'Assigned', 'Reassigned', 'Released', 'TermsSaved', 'Submitted', 'Approved', 'Rejected', 'Cancelled', 'CustomerCancelled', 'Expired', 'Withdrawn') AND \"ToStatus\" IN ('Open', 'Handling', 'PendingApproval', 'Approved', 'Expired', 'Cancelled') AND (\"FromStatus\" IS NULL OR \"FromStatus\" IN ('Open', 'Handling', 'PendingApproval', 'Approved', 'Expired', 'Cancelled')) AND (\"FromStatus\" IS NULL) = (\"Kind\" = 'Created') AND (\"ActorId\" IS NULL) = (\"Kind\" IN ('Expired', 'Withdrawn')) AND ((\"Kind\" IN ('Rejected', 'Cancelled', 'Withdrawn') AND \"Reason\" IS NOT NULL AND \"Reason\" = btrim(\"Reason\") AND length(\"Reason\") BETWEEN 1 AND 1000 AND \"Reason\" !~ '[[:cntrl:]]') OR (\"Kind\" NOT IN ('Rejected', 'Cancelled', 'Withdrawn') AND \"Reason\" IS NULL)) AND (\"ReleaseId\" IS NOT NULL) = (\"Kind\" IN ('Cancelled', 'CustomerCancelled', 'Expired', 'Withdrawn')) AND (\"Changes\" IS NULL OR (\"Kind\" IN ('TermsSaved', 'Released') AND jsonb_typeof(\"Changes\") = 'array')) AND jsonb_typeof(\"Snapshot\") = 'object' AND \"Snapshot\" ->> 'Status' = \"ToStatus\" AND (\"Snapshot\" ->> 'Version')::bigint = \"Sequence\"");
                    table.ForeignKey(
                        name: "FK_SalesOrderEvents_AspNetUsers_ActorId",
                        column: x => x.ActorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesOrderEvents_AspNetUsers_AgentId",
                        column: x => x.AgentId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesOrderEvents_AspNetUsers_PreviousAgentId",
                        column: x => x.PreviousAgentId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesOrderEvents_ReservationReleases_ReleaseId",
                        column: x => x.ReleaseId,
                        principalTable: "ReservationReleases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesOrderEvents_SalesOrders_SalesOrderId",
                        column: x => x.SalesOrderId,
                        principalTable: "SalesOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_ReservationReleases_Actor",
                table: "ReservationReleases",
                sql: "(\"Kind\" = 'CustomerCancelled' AND \"ActorId\" IS NOT NULL AND \"Reason\" IS NULL) OR (\"Kind\" IN ('StaffCancelled', 'DealCancelled') AND \"ActorId\" IS NOT NULL AND \"Reason\" IS NOT NULL AND length(btrim(\"Reason\")) BETWEEN 1 AND 1000 AND \"Reason\" = btrim(\"Reason\") AND \"Reason\" !~ '[[:cntrl:]]') OR (\"Kind\" IN ('Expired', 'Withdrawn', 'Reinspection') AND \"ActorId\" IS NULL AND \"Reason\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ReservationReleases_Shape",
                table: "ReservationReleases",
                sql: "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND isfinite(\"RecordedAt\") AND \"Refund\" = 500 AND \"Kind\" IN ('CustomerCancelled', 'StaffCancelled', 'Expired', 'Withdrawn', 'Reinspection', 'DealCancelled')");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrderEvents_ActorId",
                table: "SalesOrderEvents",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrderEvents_AgentId",
                table: "SalesOrderEvents",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrderEvents_PreviousAgentId",
                table: "SalesOrderEvents",
                column: "PreviousAgentId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrderEvents_ReleaseId",
                table: "SalesOrderEvents",
                column: "ReleaseId",
                unique: true,
                filter: "\"ReleaseId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrderEvents_SalesOrderId_Sequence",
                table: "SalesOrderEvents",
                columns: new[] { "SalesOrderId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_AgentId_Status_UpdatedAt",
                table: "SalesOrders",
                columns: new[] { "AgentId", "Status", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_BranchId_Status_DueAt",
                table: "SalesOrders",
                columns: new[] { "BranchId", "Status", "DueAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_BranchId_Status_UpdatedAt",
                table: "SalesOrders",
                columns: new[] { "BranchId", "Status", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_CustomerId",
                table: "SalesOrders",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_DecidedById",
                table: "SalesOrders",
                column: "DecidedById");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_Id_VehicleId_BranchId_CustomerId",
                table: "SalesOrders",
                columns: new[] { "Id", "VehicleId", "BranchId", "CustomerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_OrderNumber",
                table: "SalesOrders",
                column: "OrderNumber",
                unique: true,
                filter: "\"OrderNumber\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_PromoId",
                table: "SalesOrders",
                column: "PromoId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_TradeInAppointmentId",
                table: "SalesOrders",
                column: "TradeInAppointmentId",
                unique: true,
                filter: "\"TradeInAppointmentId\" IS NOT NULL AND \"Status\" IN ('Open', 'Handling', 'PendingApproval', 'Approved')");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_VehicleId",
                table: "SalesOrders",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_WarrantyPackageId",
                table: "SalesOrders",
                column: "WarrantyPackageId");

            // Every existing reservation becomes its sale: opened as it was confirmed and, when it was already released,
            // closed by the same refund receipt. Snapshots use the row's own shape, as the database records them later.
            migrationBuilder.Sql("""
                INSERT INTO "SalesOrders" ("Id", "VehicleId", "BranchId", "CustomerId", "Status", "DueAt", "PromoId",
                    "PromoCode", "BasePrice", "RoadTax", "ProcessingFee", "SstRate", "ServiceTax", "PromoDiscount",
                    "NegotiatedDiscount", "WarrantyPrice", "DepositCredit", "TradeInAllowance", "BalancePayable",
                    "CreatedAt", "UpdatedAt", "Version")
                SELECT r."Id", r."VehicleId", r."BranchId", r."CustomerId", 'Open', r."ExpiresAt", r."PromoId",
                    r."PromoCode", r."VehiclePrice", r."RoadTax", r."ProcessingFee", r."SstRate", r."ServiceTax",
                    r."PromoDiscount", 0, 0, r."Deposit", 0, r."Total" - r."Deposit", r."CreatedAt", r."CreatedAt", 1
                FROM "Reservations" r;

                INSERT INTO "SalesOrderEvents" ("Id", "SalesOrderId", "Sequence", "Kind", "ToStatus", "ActorId",
                    "Snapshot", "RecordedAt")
                SELECT gen_random_uuid(), s."Id", 1, 'Created', 'Open', s."CustomerId", to_jsonb(s), s."CreatedAt"
                FROM "SalesOrders" s;

                UPDATE "SalesOrders" s
                SET "Status" = CASE WHEN rel."Kind" = 'Expired' THEN 'Expired' ELSE 'Cancelled' END,
                    "ClosedAt" = rel."RecordedAt", "UpdatedAt" = GREATEST(rel."RecordedAt", s."CreatedAt"), "Version" = 2
                FROM "ReservationReleases" rel
                WHERE rel."ReservationId" = s."Id";

                INSERT INTO "SalesOrderEvents" ("Id", "SalesOrderId", "Sequence", "Kind", "FromStatus", "ToStatus",
                    "ActorId", "Reason", "Snapshot", "ReleaseId", "RecordedAt")
                SELECT gen_random_uuid(), s."Id", 2,
                    CASE rel."Kind" WHEN 'Expired' THEN 'Expired' WHEN 'CustomerCancelled' THEN 'CustomerCancelled'
                        WHEN 'Withdrawn' THEN 'Withdrawn' WHEN 'Reinspection' THEN 'Withdrawn' ELSE 'Cancelled' END,
                    'Open', s."Status", rel."ActorId",
                    CASE rel."Kind" WHEN 'Withdrawn' THEN 'Listing withdrawn'
                        WHEN 'Reinspection' THEN 'Vehicle sent for reinspection' ELSE rel."Reason" END,
                    to_jsonb(s), rel."Id", s."UpdatedAt"
                FROM "SalesOrders" s
                JOIN "ReservationReleases" rel ON rel."ReservationId" = s."Id";
                """);

            migrationBuilder.Sql("""
                CREATE FUNCTION forbid_sales_order_event_change() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'Sale history is append-only'
                        USING ERRCODE = '23514', CONSTRAINT = 'CK_SalesOrderEvents_Immutable';
                END $$;
                CREATE TRIGGER sales_order_events_append_only BEFORE UPDATE OR DELETE ON "SalesOrderEvents"
                    FOR EACH ROW EXECUTE FUNCTION forbid_sales_order_event_change();

                -- Each entry continues its sale's own numbering and can never be dated in the future.
                CREATE FUNCTION guard_sales_order_event() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    PERFORM pg_advisory_xact_lock(hashtextextended('sale-history:' || NEW."SalesOrderId"::text, 0));
                    IF NEW."Sequence" <> COALESCE((SELECT max("Sequence") FROM "SalesOrderEvents"
                           WHERE "SalesOrderId" = NEW."SalesOrderId"), 0) + 1 OR
                       NEW."RecordedAt" > clock_timestamp() THEN
                        RAISE EXCEPTION 'A sale history entry must continue its sale''s own history'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_SalesOrderEvents_History';
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER sales_order_event_guard BEFORE INSERT ON "SalesOrderEvents"
                    FOR EACH ROW EXECUTE FUNCTION guard_sales_order_event();

                -- A sale opens exactly as its reservation was confirmed; afterwards its origin never changes, every
                -- change advances its version, a closed sale is final and only an active agent of its branch handles it.
                CREATE FUNCTION guard_sales_order() RETURNS trigger LANGUAGE plpgsql AS $$
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
                CREATE TRIGGER sales_order_guard BEFORE INSERT OR UPDATE OR DELETE ON "SalesOrders"
                    FOR EACH ROW EXECUTE FUNCTION guard_sales_order();

                -- At commit every state of a sale has its history entry, and a closed sale has its refund receipt.
                CREATE FUNCTION require_sales_order_event() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM "SalesOrderEvents" WHERE "SalesOrderId" = NEW."Id" AND
                           "Sequence" = NEW."Version" AND "ToStatus" = NEW."Status") OR
                       (NEW."Status" IN ('Expired', 'Cancelled') AND
                           NOT EXISTS (SELECT 1 FROM "ReservationReleases" WHERE "ReservationId" = NEW."Id")) THEN
                        RAISE EXCEPTION 'Every sale state needs its history entry and a closed sale its refund'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_SalesOrders_History';
                    END IF;
                    RETURN NULL;
                END $$;
                CREATE CONSTRAINT TRIGGER sales_order_history AFTER INSERT OR UPDATE ON "SalesOrders"
                    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION require_sales_order_event();

                -- Every confirmed reservation opens its sale in the same transaction.
                CREATE FUNCTION require_reservation_sale() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM "SalesOrders" WHERE "Id" = NEW."Id") THEN
                        RAISE EXCEPTION 'A confirmed reservation must open its sale in the same transaction'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_Reservations_Sale';
                    END IF;
                    RETURN NULL;
                END $$;
                CREATE CONSTRAINT TRIGGER reservation_opens_sale AFTER INSERT ON "Reservations"
                    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION require_reservation_sale();

                -- A refund always ends its sale: at commit the sale is closed by a history entry naming this receipt.
                CREATE FUNCTION require_release_closes_sale() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM "SalesOrderEvents" e JOIN "SalesOrders" s ON s."Id" = e."SalesOrderId"
                           WHERE e."ReleaseId" = NEW."Id" AND s."Id" = NEW."ReservationId" AND
                               s."Status" IN ('Expired', 'Cancelled')) THEN
                        RAISE EXCEPTION 'A refund must close its sale in the same transaction'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_ReservationReleases_Sale';
                    END IF;
                    RETURN NULL;
                END $$;
                CREATE CONSTRAINT TRIGGER reservation_release_closes_sale AFTER INSERT ON "ReservationReleases"
                    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION require_release_closes_sale();

                -- A reservation now ends only through its sale: the deadline is the sale's, staff cancel an open sale,
                -- and a deal cancellation comes from the handling agent or a manager in scope.
                CREATE OR REPLACE FUNCTION guard_reservation_release() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE
                    held "Reservations"%ROWTYPE;
                    sale "SalesOrders"%ROWTYPE;
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
                    SELECT * INTO sale FROM "SalesOrders" WHERE "Id" = held."Id";
                    IF sale."Id" IS NULL OR sale."Status" IN ('Expired', 'Cancelled') OR
                       EXISTS (SELECT 1 FROM "ReservationReleases" WHERE "ReservationId" = held."Id") OR
                       EXISTS (SELECT 1 FROM "Reservations" WHERE "Id" = NEW."Id") THEN
                        RAISE EXCEPTION 'A reservation has only one release and full refund'
                            USING ERRCODE = '23505', CONSTRAINT = 'UX_ReservationReleases_Receipt';
                    END IF;
                    IF NEW."RecordedAt" < held."CreatedAt" OR NEW."RecordedAt" > clock_timestamp() OR
                       (NEW."Kind" = 'Expired') IS DISTINCT FROM
                           (sale."Status" = 'Open' AND NEW."RecordedAt" >= sale."DueAt") OR
                       (NEW."Kind" <> 'Expired' AND sale."Status" = 'Open' AND clock_timestamp() >= sale."DueAt") THEN
                        RAISE EXCEPTION 'Release must observe the sale''s authoritative deadline'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_ReservationReleases_Deadline';
                    END IF;
                    IF NEW."Kind" IN ('CustomerCancelled', 'StaffCancelled') AND
                       (actor."Id" IS NULL OR NOT actor."IsActive" OR sale."Status" <> 'Open' OR
                           (NEW."Kind" = 'CustomerCancelled' AND (actor."Role" <> 'Customer' OR actor."Id" <> held."CustomerId")) OR
                           (NEW."Kind" = 'StaffCancelled' AND (actor."Role" NOT IN ('BranchManager', 'GeneralManager') OR
                               (actor."Role" = 'BranchManager' AND actor."BranchId" IS DISTINCT FROM held."BranchId")))) THEN
                        RAISE EXCEPTION 'Cancelling an open sale requires its customer or a scoped manager'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_ReservationReleases_Actor';
                    END IF;
                    IF NEW."Kind" = 'DealCancelled' AND (actor."Id" IS NULL OR NOT actor."IsActive" OR
                        sale."Status" NOT IN ('Handling', 'PendingApproval', 'Approved') OR NOT (
                            (actor."Role" = 'SalesAgent' AND sale."AgentId" = actor."Id" AND sale."Status" = 'Handling') OR
                            (actor."Role" = 'BranchManager' AND actor."BranchId" IS NOT DISTINCT FROM held."BranchId") OR
                            actor."Role" = 'GeneralManager')) THEN
                        RAISE EXCEPTION 'Cancelling a handled sale requires its agent while in handling or a scoped manager'
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

                -- Expiry, withdrawal and reinspection end the sale too: the refund, the closed sale and its history
                -- entry are written together, in the sale's own snapshot shape.
                CREATE OR REPLACE FUNCTION release_unavailable_reservation(vehicle_id uuid) RETURNS void LANGUAGE plpgsql AS $$
                DECLARE
                    held "Reservations"%ROWTYPE;
                    sale "SalesOrders"%ROWTYPE;
                    closed "SalesOrders"%ROWTYPE;
                    release_kind text;
                    release_id uuid;
                    recorded_at timestamptz;
                BEGIN
                    PERFORM 1 FROM "Vehicles" WHERE "Id" = vehicle_id FOR UPDATE;
                    SELECT * INTO held FROM "Reservations" WHERE "VehicleId" = vehicle_id AND "IsActive";
                    IF held."Id" IS NULL THEN
                        RETURN;
                    END IF;
                    SELECT * INTO sale FROM "SalesOrders" WHERE "Id" = held."Id" FOR UPDATE;
                    recorded_at := clock_timestamp();
                    IF sale."Status" = 'Open' AND recorded_at >= sale."DueAt" THEN
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
                    release_id := gen_random_uuid();
                    INSERT INTO "ReservationReleases" ("Id", "ReservationId", "Kind", "RecordedAt", "Refund")
                        VALUES (release_id, held."Id", release_kind, recorded_at, held."Deposit");
                    UPDATE "SalesOrders" SET
                        "Status" = CASE WHEN release_kind = 'Expired' THEN 'Expired' ELSE 'Cancelled' END,
                        "AgentId" = NULL, "ClaimedAt" = NULL, "PausedSeconds" = NULL,
                        "ClosedAt" = recorded_at, "UpdatedAt" = GREATEST(recorded_at, sale."UpdatedAt"),
                        "Version" = sale."Version" + 1
                    WHERE "Id" = sale."Id"
                    RETURNING * INTO closed;
                    INSERT INTO "SalesOrderEvents" ("Id", "SalesOrderId", "Sequence", "Kind", "FromStatus", "ToStatus",
                        "PreviousAgentId", "Reason", "Snapshot", "ReleaseId", "RecordedAt")
                    VALUES (gen_random_uuid(), closed."Id", closed."Version",
                        CASE WHEN release_kind = 'Expired' THEN 'Expired' ELSE 'Withdrawn' END,
                        sale."Status", closed."Status", sale."AgentId",
                        CASE release_kind WHEN 'Withdrawn' THEN 'Listing withdrawn'
                            WHEN 'Reinspection' THEN 'Vehicle sent for reinspection' END,
                        to_jsonb(closed), release_id, recorded_at);
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restores the reservation-only release rules; a deal-cancelled refund cannot be expressed without sales.
            migrationBuilder.Sql("""
                DROP TRIGGER reservation_opens_sale ON "Reservations";
                DROP FUNCTION require_reservation_sale();
                DROP TRIGGER reservation_release_closes_sale ON "ReservationReleases";
                DROP FUNCTION require_release_closes_sale();
                DROP TRIGGER sales_order_history ON "SalesOrders";
                DROP FUNCTION require_sales_order_event();
                DROP TRIGGER sales_order_guard ON "SalesOrders";
                DROP FUNCTION guard_sales_order();
                DROP TRIGGER sales_order_event_guard ON "SalesOrderEvents";
                DROP FUNCTION guard_sales_order_event();
                DROP TRIGGER sales_order_events_append_only ON "SalesOrderEvents";
                DROP FUNCTION forbid_sales_order_event_change();

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

            migrationBuilder.DropTable(
                name: "DealNumberCounters");

            migrationBuilder.DropTable(
                name: "SalesOrderEvents");

            migrationBuilder.DropTable(
                name: "SalesOrders");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Reservations_Id_VehicleId_BranchId_CustomerId",
                table: "Reservations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ReservationReleases_Actor",
                table: "ReservationReleases");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ReservationReleases_Shape",
                table: "ReservationReleases");

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

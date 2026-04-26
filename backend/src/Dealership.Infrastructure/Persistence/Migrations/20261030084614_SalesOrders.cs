using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dealership.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SalesOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                    OrderNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true, collation: "C"),
                    VehicleId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    SalesAgentId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    TradeInAllowance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    BalancePayable = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountLimitAtSubmit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    DecidedById = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SubmittedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesOrders", x => x.Id);
                    table.CheckConstraint("CK_SalesOrders_Approval", "(\"Status\" = 'Draft' AND \"DecidedById\" IS NULL AND \"DiscountLimitAtSubmit\" IS NULL) OR (\"Status\" = 'PendingManagerApproval' AND \"DecidedById\" IS NULL AND \"DiscountLimitAtSubmit\" IS NOT NULL AND \"NegotiatedDiscount\" > \"DiscountLimitAtSubmit\") OR (\"Status\" = 'Approved' AND \"DiscountLimitAtSubmit\" IS NOT NULL AND (\"DecidedById\" IS NOT NULL OR \"NegotiatedDiscount\" <= \"DiscountLimitAtSubmit\") AND \"DecidedById\" IS DISTINCT FROM \"SalesAgentId\") OR \"Status\" = 'Cancelled'");
                    table.CheckConstraint("CK_SalesOrders_Price", "\"BasePrice\" > 0 AND \"RoadTax\" >= 0 AND \"ProcessingFee\" >= 0 AND \"SstRate\" BETWEEN 0 AND 1 AND \"PromoDiscount\" >= 0 AND \"NegotiatedDiscount\" >= 0 AND \"WarrantyPrice\" >= 0 AND \"DepositCredit\" >= 0 AND \"TradeInAllowance\" >= 0 AND \"PromoDiscount\" + \"NegotiatedDiscount\" <= \"BasePrice\" AND \"ServiceTax\" = round(\"ProcessingFee\" * \"SstRate\", 2) AND \"BalancePayable\" >= 0 AND \"BalancePayable\" = \"BasePrice\" - \"PromoDiscount\" - \"NegotiatedDiscount\" + \"ProcessingFee\" + \"RoadTax\" + \"ServiceTax\" + \"WarrantyPrice\" - \"DepositCredit\" - \"TradeInAllowance\"");
                    table.CheckConstraint("CK_SalesOrders_Shape", "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"Version\" > 0 AND \"Status\" IN ('Draft', 'PendingManagerApproval', 'Approved', 'Cancelled') AND isfinite(\"CreatedAtUtc\") AND isfinite(\"UpdatedAtUtc\") AND \"UpdatedAtUtc\" >= \"CreatedAtUtc\" AND ((\"OrderNumber\" IS NULL AND \"SubmittedAtUtc\" IS NULL) OR (\"OrderNumber\" ~ '^VPO-[0-9]{4}-[0-9]{6}$' AND \"SubmittedAtUtc\" IS NOT NULL))");
                    table.CheckConstraint("CK_SalesOrders_Terms", "((\"PromoId\" IS NULL AND \"PromoCode\" IS NULL AND \"PromoDiscount\" = 0) OR (\"PromoId\" IS NOT NULL AND \"PromoCode\" IS NOT NULL)) AND ((\"WarrantyPackageId\" IS NULL AND \"WarrantyName\" IS NULL AND \"WarrantyPrice\" = 0) OR (\"WarrantyPackageId\" IS NOT NULL AND \"WarrantyName\" IS NOT NULL)) AND ((\"TradeInAppointmentId\" IS NULL AND \"TradeInAllowance\" = 0) OR \"TradeInAppointmentId\" IS NOT NULL) AND (\"ReservationId\" IS NOT NULL OR \"DepositCredit\" = 0)");
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
                        name: "FK_SalesOrders_AspNetUsers_SalesAgentId",
                        column: x => x.SalesAgentId,
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
                        name: "FK_SalesOrders_Reservations_ReservationId",
                        column: x => x.ReservationId,
                        principalTable: "Reservations",
                        principalColumn: "Id",
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
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    FromStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    ToStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    RecordedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesOrderEvents", x => x.Id);
                    table.CheckConstraint("CK_SalesOrderEvents_Shape", "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND isfinite(\"RecordedAtUtc\") AND \"Kind\" IN ('Created', 'Revised', 'Submitted', 'Approved', 'Rejected', 'Cancelled') AND \"ToStatus\" IN ('Draft', 'PendingManagerApproval', 'Approved', 'Cancelled') AND (\"FromStatus\" IS NULL OR \"FromStatus\" IN ('Draft', 'PendingManagerApproval', 'Approved', 'Cancelled')) AND ((\"Kind\" IN ('Rejected', 'Cancelled') AND \"Reason\" IS NOT NULL AND \"Reason\" = btrim(\"Reason\") AND length(\"Reason\") BETWEEN 1 AND 1000 AND \"Reason\" !~ '[[:cntrl:]]') OR (\"Kind\" NOT IN ('Rejected', 'Cancelled') AND \"Reason\" IS NULL))");
                    table.ForeignKey(
                        name: "FK_SalesOrderEvents_AspNetUsers_ActorId",
                        column: x => x.ActorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesOrderEvents_SalesOrders_SalesOrderId",
                        column: x => x.SalesOrderId,
                        principalTable: "SalesOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrderEvents_ActorId",
                table: "SalesOrderEvents",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrderEvents_SalesOrderId_RecordedAtUtc_Id",
                table: "SalesOrderEvents",
                columns: new[] { "SalesOrderId", "RecordedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_BranchId_CreatedAtUtc_Id",
                table: "SalesOrders",
                columns: new[] { "BranchId", "CreatedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_CustomerId_CreatedAtUtc_Id",
                table: "SalesOrders",
                columns: new[] { "CustomerId", "CreatedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_DecidedById",
                table: "SalesOrders",
                column: "DecidedById");

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
                name: "IX_SalesOrders_ReservationId",
                table: "SalesOrders",
                column: "ReservationId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_SalesAgentId_CreatedAtUtc_Id",
                table: "SalesOrders",
                columns: new[] { "SalesAgentId", "CreatedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_TradeInAppointmentId",
                table: "SalesOrders",
                column: "TradeInAppointmentId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_VehicleId",
                table: "SalesOrders",
                column: "VehicleId",
                unique: true,
                filter: "\"Status\" IN ('Draft', 'PendingManagerApproval', 'Approved')");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_WarrantyPackageId",
                table: "SalesOrders",
                column: "WarrantyPackageId");

            // Deal history is evidence: no privilege level can rewrite or remove a recorded event.
            migrationBuilder.Sql("""
                CREATE FUNCTION forbid_sales_order_event_change() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'Sales order events are append-only.' USING ERRCODE = 'restrict_violation';
                END $$;
                CREATE TRIGGER sales_order_events_append_only BEFORE UPDATE OR DELETE ON "SalesOrderEvents"
                    FOR EACH ROW EXECUTE FUNCTION forbid_sales_order_event_change();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER sales_order_events_append_only ON "SalesOrderEvents";
                DROP FUNCTION forbid_sales_order_event_change();
                """);

            migrationBuilder.DropTable(
                name: "DealNumberCounters");

            migrationBuilder.DropTable(
                name: "SalesOrderEvents");

            migrationBuilder.DropTable(
                name: "SalesOrders");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dealership.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BillplzDepositPayments : Migration
    {
        private static readonly string[] CustomerStateColumns = ["CustomerId", "State"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ReservationDepositRequests_Shape",
                table: "ReservationDepositRequests");

            migrationBuilder.AlterColumn<Guid>(
                name: "BookedByStaffId",
                table: "ReservationDepositRequests",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.CreateTable(
                name: "ReservationDepositPayments",
                columns: table => new
                {
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    BillId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    BillUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    AmountSen = table.Column<int>(type: "integer", nullable: false),
                    State = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PaidAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PaidAmountSen = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReservationDepositPayments", x => x.RequestId);
                    table.CheckConstraint("CK_ReservationDepositPayments_Shape", "\"AmountSen\" > 0 AND isfinite(\"CreatedAt\") AND (\"State\" = 'Pending' AND \"PaidAt\" IS NULL AND \"PaidAmountSen\" IS NULL OR \"State\" IN ('Paid', 'PaidUnmatched') AND isfinite(\"PaidAt\") AND \"PaidAt\" >= \"CreatedAt\" AND \"PaidAmountSen\" >= 0)");
                    table.ForeignKey(
                        name: "FK_ReservationDepositPayments_ReservationDepositRequests_Reque~",
                        column: x => x.RequestId,
                        principalTable: "ReservationDepositRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReservationRefunds",
                columns: table => new
                {
                    PaymentRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    AmountSen = table.Column<int>(type: "integer", nullable: false),
                    State = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Attempt = table.Column<int>(type: "integer", nullable: false),
                    BankCode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    AccountLast4 = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    AccountHolder = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PayoutId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SettledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReservationRefunds", x => x.PaymentRequestId);
                    table.CheckConstraint("CK_ReservationRefunds_Shape", "\"AmountSen\" > 0 AND \"Attempt\" >= 1 AND isfinite(\"RequestedAt\") AND \"State\" IN ('Processing', 'Completed', 'Failed') AND (\"State\" = 'Processing' AND \"SettledAt\" IS NULL OR \"State\" <> 'Processing' AND isfinite(\"SettledAt\") AND \"SettledAt\" >= \"RequestedAt\")");
                    table.ForeignKey(
                        name: "FK_ReservationRefunds_CustomerProfiles_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "CustomerProfiles",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReservationRefunds_ReservationDepositPayments_PaymentReques~",
                        column: x => x.PaymentRequestId,
                        principalTable: "ReservationDepositPayments",
                        principalColumn: "RequestId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_ReservationDepositRequests_Shape",
                table: "ReservationDepositRequests",
                sql: "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND isfinite(\"CreatedAt\") AND isfinite(\"ExpiresAt\") AND (\"BookedByStaffId\" IS NOT NULL AND \"ExpiresAt\" = \"CreatedAt\" + interval '24 hours' OR \"BookedByStaffId\" IS NULL AND \"ExpiresAt\" = \"CreatedAt\" + interval '15 minutes') AND (\"ConfirmedAt\" IS NULL OR (isfinite(\"ConfirmedAt\") AND \"ConfirmedAt\" >= \"CreatedAt\" AND \"ConfirmedAt\" < \"ExpiresAt\")) AND (\"CancelledAt\" IS NULL OR (\"ConfirmedAt\" IS NULL AND isfinite(\"CancelledAt\") AND \"CancelledAt\" >= \"CreatedAt\" AND \"CancelledAt\" < \"ExpiresAt\"))");

            migrationBuilder.CreateIndex(
                name: "IX_ReservationDepositPayments_BillId",
                table: "ReservationDepositPayments",
                column: "BillId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReservationRefunds_CustomerId_State",
                table: "ReservationRefunds",
                columns: CustomerStateColumns);

            migrationBuilder.CreateIndex(
                name: "IX_ReservationRefunds_PayoutId",
                table: "ReservationRefunds",
                column: "PayoutId",
                unique: true,
                filter: "\"PayoutId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReservationRefunds");

            migrationBuilder.DropTable(
                name: "ReservationDepositPayments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ReservationDepositRequests_Shape",
                table: "ReservationDepositRequests");

            migrationBuilder.AlterColumn<Guid>(
                name: "BookedByStaffId",
                table: "ReservationDepositRequests",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ReservationDepositRequests_Shape",
                table: "ReservationDepositRequests",
                sql: "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND isfinite(\"CreatedAt\") AND isfinite(\"ExpiresAt\") AND \"ExpiresAt\" = \"CreatedAt\" + interval '24 hours' AND (\"ConfirmedAt\" IS NULL OR (isfinite(\"ConfirmedAt\") AND \"ConfirmedAt\" >= \"CreatedAt\" AND \"ConfirmedAt\" < \"ExpiresAt\")) AND (\"CancelledAt\" IS NULL OR (\"ConfirmedAt\" IS NULL AND isfinite(\"CancelledAt\") AND \"CancelledAt\" >= \"CreatedAt\" AND \"CancelledAt\" < \"ExpiresAt\"))");
        }
    }
}

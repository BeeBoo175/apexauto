using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dealership.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BillplzDepositPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.CreateIndex(
                name: "IX_ReservationDepositPayments_BillId",
                table: "ReservationDepositPayments",
                column: "BillId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReservationDepositPayments");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dealership.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UnmatchedPaymentMail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReservationUnmatchedPaymentMailOutbox",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeliveryAttempts = table.Column<int>(type: "integer", nullable: false),
                    NextDeliveryAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeliveryClaimId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeliveryClaimExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeliveredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReservationUnmatchedPaymentMailOutbox", x => x.Id);
                    table.CheckConstraint("CK_ReservationUnmatchedPaymentMailOutbox_Shape", "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"DeliveryAttempts\" >= 0");
                    table.ForeignKey(
                        name: "FK_ReservationUnmatchedPaymentMailOutbox_ReservationDepositPay~",
                        column: x => x.RequestId,
                        principalTable: "ReservationDepositPayments",
                        principalColumn: "RequestId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReservationUnmatchedPaymentMailOutbox_NextDeliveryAt_Id",
                table: "ReservationUnmatchedPaymentMailOutbox",
                columns: new[] { "NextDeliveryAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_ReservationUnmatchedPaymentMailOutbox_RequestId",
                table: "ReservationUnmatchedPaymentMailOutbox",
                column: "RequestId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReservationUnmatchedPaymentMailOutbox");
        }
    }
}

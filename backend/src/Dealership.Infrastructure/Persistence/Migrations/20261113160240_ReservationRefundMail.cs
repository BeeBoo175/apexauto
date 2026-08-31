using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dealership.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReservationRefundMail : Migration
    {
        private static readonly string[] DeliveryIndexColumns = ["NextDeliveryAt", "Id"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReservationRefundMailOutbox",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RefundId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeliveryAttempts = table.Column<int>(type: "integer", nullable: false),
                    NextDeliveryAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeliveryClaimId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeliveryClaimExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeliveredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReservationRefundMailOutbox", x => x.Id);
                    table.CheckConstraint("CK_ReservationRefundMailOutbox_Shape", "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"DeliveryAttempts\" >= 0");
                    table.ForeignKey(
                        name: "FK_ReservationRefundMailOutbox_ReservationRefunds_RefundId",
                        column: x => x.RefundId,
                        principalTable: "ReservationRefunds",
                        principalColumn: "PaymentRequestId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReservationRefundMailOutbox_NextDeliveryAt_Id",
                table: "ReservationRefundMailOutbox",
                columns: DeliveryIndexColumns);

            migrationBuilder.CreateIndex(
                name: "IX_ReservationRefundMailOutbox_RefundId",
                table: "ReservationRefundMailOutbox",
                column: "RefundId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReservationRefundMailOutbox");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dealership.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReservationDepositRequests : Migration
    {
        private static readonly string[] ListingPublicationIdentityColumns = ["Id", "VehicleId", "BranchId"];
        private static readonly string[] OutboxNextDeliveryColumns = ["NextDeliveryAt", "Id"];
        private static readonly string[] CustomerExpiresColumns = ["CustomerId", "ExpiresAt"];
        private static readonly string[] PublicationVehicleBranchColumns = ["PublicationId", "VehicleId", "BranchId"];
        private static readonly string[] VehicleExpiresColumns = ["VehicleId", "ExpiresAt"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReservationDepositRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    PublicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PromoId = table.Column<Guid>(type: "uuid", nullable: true),
                    BookedByStaffId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConfirmedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancelledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReservationDepositRequests", x => x.Id);
                    table.CheckConstraint("CK_ReservationDepositRequests_Shape", "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND isfinite(\"CreatedAt\") AND isfinite(\"ExpiresAt\") AND \"ExpiresAt\" = \"CreatedAt\" + interval '24 hours' AND (\"ConfirmedAt\" IS NULL OR (isfinite(\"ConfirmedAt\") AND \"ConfirmedAt\" >= \"CreatedAt\" AND \"ConfirmedAt\" < \"ExpiresAt\")) AND (\"CancelledAt\" IS NULL OR (\"ConfirmedAt\" IS NULL AND isfinite(\"CancelledAt\") AND \"CancelledAt\" >= \"CreatedAt\" AND \"CancelledAt\" < \"ExpiresAt\"))");
                    table.ForeignKey(
                        name: "FK_ReservationDepositRequests_AspNetUsers_BookedByStaffId",
                        column: x => x.BookedByStaffId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReservationDepositRequests_CustomerProfiles_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "CustomerProfiles",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReservationDepositRequests_ListingPublications_PublicationI~",
                        columns: x => new { x.PublicationId, x.VehicleId, x.BranchId },
                        principalTable: "ListingPublications",
                        principalColumns: ListingPublicationIdentityColumns,
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReservationDepositRequests_PromoCodes_PromoId",
                        column: x => x.PromoId,
                        principalTable: "PromoCodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReservationDepositMailOutbox",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    DeliveryAttempts = table.Column<int>(type: "integer", nullable: false),
                    NextDeliveryAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeliveryClaimId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeliveryClaimExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeliveredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReservationDepositMailOutbox", x => x.Id);
                    table.CheckConstraint("CK_ReservationDepositMailOutbox_Shape", "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"DeliveryAttempts\" >= 0 AND \"RecipientEmail\" ~ '[^[:space:]]'");
                    table.ForeignKey(
                        name: "FK_ReservationDepositMailOutbox_ReservationDepositRequests_Req~",
                        column: x => x.RequestId,
                        principalTable: "ReservationDepositRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReservationDepositMailOutbox_NextDeliveryAt_Id",
                table: "ReservationDepositMailOutbox",
                columns: OutboxNextDeliveryColumns);

            migrationBuilder.CreateIndex(
                name: "IX_ReservationDepositMailOutbox_RequestId",
                table: "ReservationDepositMailOutbox",
                column: "RequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReservationDepositRequests_BookedByStaffId",
                table: "ReservationDepositRequests",
                column: "BookedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_ReservationDepositRequests_CustomerId_ExpiresAt",
                table: "ReservationDepositRequests",
                columns: CustomerExpiresColumns,
                filter: "\"ConfirmedAt\" IS NULL AND \"CancelledAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ReservationDepositRequests_PromoId",
                table: "ReservationDepositRequests",
                column: "PromoId");

            migrationBuilder.CreateIndex(
                name: "IX_ReservationDepositRequests_PublicationId_VehicleId_BranchId",
                table: "ReservationDepositRequests",
                columns: PublicationVehicleBranchColumns);

            migrationBuilder.CreateIndex(
                name: "IX_ReservationDepositRequests_VehicleId_ExpiresAt",
                table: "ReservationDepositRequests",
                columns: VehicleExpiresColumns,
                filter: "\"ConfirmedAt\" IS NULL AND \"CancelledAt\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReservationDepositMailOutbox");

            migrationBuilder.DropTable(
                name: "ReservationDepositRequests");
        }
    }
}

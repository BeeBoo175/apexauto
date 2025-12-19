using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dealership.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EmailVerification : Migration
    {
        private static readonly string[] UserCreatedIndexColumns = ["UserId", "CreatedAt"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmailVerificationChallenges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientEmail = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    InvalidatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConsumedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeliveryAttempts = table.Column<int>(type: "integer", nullable: false),
                    NextDeliveryAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeliveryClaimId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeliveryClaimExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeliveredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsResend = table.Column<bool>(type: "boolean", nullable: false),
                    ResendKey = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailVerificationChallenges", x => x.Id);
                    table.CheckConstraint("CK_EmailVerificationChallenges_Attempts", "\"DeliveryAttempts\" >= 0");
                    table.CheckConstraint("CK_EmailVerificationChallenges_Lifetime", "\"ExpiresAt\" > \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_EmailVerificationChallenges_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmailVerificationChallenges_NextDeliveryAt",
                table: "EmailVerificationChallenges",
                column: "NextDeliveryAt");

            migrationBuilder.CreateIndex(
                name: "IX_EmailVerificationChallenges_ResendKey",
                table: "EmailVerificationChallenges",
                column: "ResendKey",
                unique: true,
                filter: "\"ResendKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EmailVerificationChallenges_UserId",
                table: "EmailVerificationChallenges",
                column: "UserId",
                unique: true,
                filter: "\"InvalidatedAt\" IS NULL AND \"ConsumedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EmailVerificationChallenges_UserId_CreatedAt",
                table: "EmailVerificationChallenges",
                columns: UserCreatedIndexColumns);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmailVerificationChallenges");
        }
    }
}

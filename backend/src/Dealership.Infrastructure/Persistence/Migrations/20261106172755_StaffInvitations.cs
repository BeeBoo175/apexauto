using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dealership.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StaffInvitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StaffInvitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    Role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Mobile = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    InvitedById = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TokenNonce = table.Column<Guid>(type: "uuid", nullable: false),
                    SentAt = table.Column<DateTimeOffset[]>(type: "timestamp with time zone[]", nullable: false),
                    ActivatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ActivatedUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RevokedById = table.Column<Guid>(type: "uuid", nullable: true),
                    DeliveryAttempts = table.Column<int>(type: "integer", nullable: false),
                    NextDeliveryAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeliveryClaimId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeliveryClaimExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeliveredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    NormalizedEmail = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true, computedColumnSql: "upper(\"Email\")", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffInvitations", x => x.Id);
                    table.CheckConstraint("CK_StaffInvitations_Lifetime", "\"ExpiresAt\" > \"CreatedAt\" AND \"DeliveryAttempts\" >= 0");
                    table.CheckConstraint("CK_StaffInvitations_Outcome", "(\"ActivatedAt\" IS NULL OR \"RevokedAt\" IS NULL) AND (\"ActivatedAt\" IS NULL) = (\"ActivatedUserId\" IS NULL) AND (\"RevokedAt\" IS NULL) = (\"RevokedById\" IS NULL)");
                    table.CheckConstraint("CK_StaffInvitations_Profile", "\"Name\" ~ '[^[:space:]]' AND \"Mobile\" ~ '[^[:space:]]'");
                    table.CheckConstraint("CK_StaffInvitations_Role", "\"Role\" IN ('SalesAgent', 'WorkshopTechnician', 'BranchManager', 'GeneralManager', 'Administrator') AND ((\"Role\" IN ('SalesAgent', 'WorkshopTechnician', 'BranchManager') AND \"BranchId\" IS NOT NULL) OR (\"Role\" IN ('GeneralManager', 'Administrator') AND \"BranchId\" IS NULL))");
                    table.ForeignKey(
                        name: "FK_StaffInvitations_AspNetUsers_ActivatedUserId",
                        column: x => x.ActivatedUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StaffInvitations_AspNetUsers_InvitedById",
                        column: x => x.InvitedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StaffInvitations_AspNetUsers_RevokedById",
                        column: x => x.RevokedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StaffInvitations_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StaffInvitations_ActivatedUserId",
                table: "StaffInvitations",
                column: "ActivatedUserId",
                unique: true,
                filter: "\"ActivatedUserId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StaffInvitations_BranchId",
                table: "StaffInvitations",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffInvitations_InvitedById",
                table: "StaffInvitations",
                column: "InvitedById");

            migrationBuilder.CreateIndex(
                name: "IX_StaffInvitations_NextDeliveryAt",
                table: "StaffInvitations",
                column: "NextDeliveryAt");

            migrationBuilder.CreateIndex(
                name: "IX_StaffInvitations_RevokedById",
                table: "StaffInvitations",
                column: "RevokedById");

            migrationBuilder.CreateIndex(
                name: "StaffInvitationPendingEmailIndex",
                table: "StaffInvitations",
                column: "NormalizedEmail",
                unique: true,
                filter: "\"ActivatedAt\" IS NULL AND \"RevokedAt\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StaffInvitations");
        }
    }
}

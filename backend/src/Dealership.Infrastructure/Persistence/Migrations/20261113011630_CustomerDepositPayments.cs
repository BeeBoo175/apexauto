using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dealership.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CustomerDepositPayments : Migration
    {
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

            migrationBuilder.AddCheckConstraint(
                name: "CK_ReservationDepositRequests_Shape",
                table: "ReservationDepositRequests",
                sql: "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND isfinite(\"CreatedAt\") AND isfinite(\"ExpiresAt\") AND (\"BookedByStaffId\" IS NOT NULL AND \"ExpiresAt\" = \"CreatedAt\" + interval '24 hours' OR \"BookedByStaffId\" IS NULL AND \"ExpiresAt\" = \"CreatedAt\" + interval '15 minutes') AND (\"ConfirmedAt\" IS NULL OR (isfinite(\"ConfirmedAt\") AND \"ConfirmedAt\" >= \"CreatedAt\" AND \"ConfirmedAt\" < \"ExpiresAt\")) AND (\"CancelledAt\" IS NULL OR (\"ConfirmedAt\" IS NULL AND isfinite(\"CancelledAt\") AND \"CancelledAt\" >= \"CreatedAt\" AND \"CancelledAt\" < \"ExpiresAt\"))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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

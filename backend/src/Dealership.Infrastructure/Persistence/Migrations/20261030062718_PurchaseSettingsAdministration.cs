using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dealership.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PurchaseSettingsAdministration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PurchaseSettings_Shape",
                table: "PurchaseSettings");

            migrationBuilder.AddColumn<decimal>(
                name: "MaxAgentDiscountAmount",
                table: "PurchaseSettings",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 500m);

            migrationBuilder.AddColumn<int>(
                name: "ReservationHoldHours",
                table: "PurchaseSettings",
                type: "integer",
                nullable: false,
                defaultValue: 48);

            migrationBuilder.AddCheckConstraint(
                name: "CK_PurchaseSettings_Shape",
                table: "PurchaseSettings",
                sql: "\"Id\" = 1 AND \"ProcessingFee\" BETWEEN 0 AND 5000 AND \"SstRate\" BETWEEN 0 AND 0.20 AND \"MaxAgentDiscountAmount\" BETWEEN 0 AND 5000 AND \"ReservationHoldHours\" BETWEEN 1 AND 168 AND \"Version\" > 0 AND isfinite(\"UpdatedAtUtc\") AND \"UpdatedById\" <> '00000000-0000-0000-0000-000000000000'::uuid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PurchaseSettings_Shape",
                table: "PurchaseSettings");

            migrationBuilder.DropColumn(
                name: "MaxAgentDiscountAmount",
                table: "PurchaseSettings");

            migrationBuilder.DropColumn(
                name: "ReservationHoldHours",
                table: "PurchaseSettings");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PurchaseSettings_Shape",
                table: "PurchaseSettings",
                sql: "\"Id\" = 1 AND \"ProcessingFee\" >= 0 AND \"ProcessingFee\" <> 'NaN'::numeric AND \"SstRate\" BETWEEN 0 AND 1 AND \"Version\" > 0 AND isfinite(\"UpdatedAtUtc\") AND \"UpdatedById\" <> '00000000-0000-0000-0000-000000000000'::uuid");
        }
    }
}

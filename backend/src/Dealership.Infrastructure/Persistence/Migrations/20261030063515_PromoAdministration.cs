using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dealership.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PromoAdministration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PromoCodes_Shape",
                table: "PromoCodes");

            migrationBuilder.AddColumn<long>(
                name: "Version",
                table: "PromoCodes",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.AddCheckConstraint(
                name: "CK_PromoCodes_Shape",
                table: "PromoCodes",
                sql: "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND length(\"Code\") > 0 AND \"Discount\" > 0 AND \"Discount\" <= 100000 AND \"Version\" > 0 AND isfinite(\"ExpiresAtUtc\")");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PromoCodes_Shape",
                table: "PromoCodes");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "PromoCodes");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PromoCodes_Shape",
                table: "PromoCodes",
                sql: "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND length(\"Code\") > 0 AND \"Discount\" >= 0 AND \"Discount\" <> 'NaN'::numeric AND isfinite(\"ExpiresAtUtc\")");
        }
    }
}

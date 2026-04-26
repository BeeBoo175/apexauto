using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dealership.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WarrantyPackages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WarrantyPackages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, collation: "C"),
                    DurationMonths = table.Column<int>(type: "integer", nullable: false),
                    MileageCapKm = table.Column<int>(type: "integer", nullable: false),
                    Price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    MinimumGrade = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WarrantyPackages", x => x.Id);
                    table.CheckConstraint("CK_WarrantyPackages_Shape", "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND length(btrim(\"Name\")) > 0 AND \"Name\" = btrim(\"Name\") AND \"DurationMonths\" BETWEEN 1 AND 120 AND \"MileageCapKm\" BETWEEN 1 AND 500000 AND \"Price\" BETWEEN 0 AND 20000 AND \"MinimumGrade\" IN ('A', 'B', 'C') AND \"Version\" > 0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_WarrantyPackages_Name",
                table: "WarrantyPackages",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WarrantyPackages");
        }
    }
}

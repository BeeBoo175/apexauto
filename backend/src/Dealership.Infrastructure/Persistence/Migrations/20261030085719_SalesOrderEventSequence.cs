using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dealership.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SalesOrderEventSequence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SalesOrderEvents_SalesOrderId_RecordedAtUtc_Id",
                table: "SalesOrderEvents");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SalesOrderEvents_Shape",
                table: "SalesOrderEvents");

            migrationBuilder.AddColumn<long>(
                name: "Sequence",
                table: "SalesOrderEvents",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrderEvents_SalesOrderId_Sequence",
                table: "SalesOrderEvents",
                columns: new[] { "SalesOrderId", "Sequence" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_SalesOrderEvents_Shape",
                table: "SalesOrderEvents",
                sql: "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND isfinite(\"RecordedAtUtc\") AND \"Sequence\" > 0 AND \"Kind\" IN ('Created', 'Revised', 'Submitted', 'Approved', 'Rejected', 'Cancelled') AND \"ToStatus\" IN ('Draft', 'PendingManagerApproval', 'Approved', 'Cancelled') AND (\"FromStatus\" IS NULL OR \"FromStatus\" IN ('Draft', 'PendingManagerApproval', 'Approved', 'Cancelled')) AND ((\"Kind\" IN ('Rejected', 'Cancelled') AND \"Reason\" IS NOT NULL AND \"Reason\" = btrim(\"Reason\") AND length(\"Reason\") BETWEEN 1 AND 1000 AND \"Reason\" !~ '[[:cntrl:]]') OR (\"Kind\" NOT IN ('Rejected', 'Cancelled') AND \"Reason\" IS NULL))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SalesOrderEvents_SalesOrderId_Sequence",
                table: "SalesOrderEvents");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SalesOrderEvents_Shape",
                table: "SalesOrderEvents");

            migrationBuilder.DropColumn(
                name: "Sequence",
                table: "SalesOrderEvents");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrderEvents_SalesOrderId_RecordedAtUtc_Id",
                table: "SalesOrderEvents",
                columns: new[] { "SalesOrderId", "RecordedAtUtc", "Id" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_SalesOrderEvents_Shape",
                table: "SalesOrderEvents",
                sql: "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND isfinite(\"RecordedAtUtc\") AND \"Kind\" IN ('Created', 'Revised', 'Submitted', 'Approved', 'Rejected', 'Cancelled') AND \"ToStatus\" IN ('Draft', 'PendingManagerApproval', 'Approved', 'Cancelled') AND (\"FromStatus\" IS NULL OR \"FromStatus\" IN ('Draft', 'PendingManagerApproval', 'Approved', 'Cancelled')) AND ((\"Kind\" IN ('Rejected', 'Cancelled') AND \"Reason\" IS NOT NULL AND \"Reason\" = btrim(\"Reason\") AND length(\"Reason\") BETWEEN 1 AND 1000 AND \"Reason\" !~ '[[:cntrl:]]') OR (\"Kind\" NOT IN ('Rejected', 'Cancelled') AND \"Reason\" IS NULL))");
        }
    }
}

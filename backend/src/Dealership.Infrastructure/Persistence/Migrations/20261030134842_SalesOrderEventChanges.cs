using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dealership.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SalesOrderEventChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_SalesOrderEvents_Shape",
                table: "SalesOrderEvents");

            migrationBuilder.AddColumn<string>(
                name: "Changes",
                table: "SalesOrderEvents",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_SalesOrderEvents_Shape",
                table: "SalesOrderEvents",
                sql: "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND isfinite(\"RecordedAtUtc\") AND \"Sequence\" > 0 AND \"Kind\" IN ('Created', 'Revised', 'Submitted', 'Approved', 'Rejected', 'Cancelled', 'Reassigned') AND \"ToStatus\" IN ('Draft', 'PendingManagerApproval', 'Approved', 'Cancelled') AND (\"FromStatus\" IS NULL OR \"FromStatus\" IN ('Draft', 'PendingManagerApproval', 'Approved', 'Cancelled')) AND ((\"Kind\" IN ('Rejected', 'Cancelled') AND \"Reason\" IS NOT NULL AND \"Reason\" = btrim(\"Reason\") AND length(\"Reason\") BETWEEN 1 AND 1000 AND \"Reason\" !~ '[[:cntrl:]]') OR (\"Kind\" NOT IN ('Rejected', 'Cancelled') AND \"Reason\" IS NULL)) AND (\"Changes\" IS NULL OR (\"Kind\" = 'Revised' AND jsonb_typeof(\"Changes\") = 'array'))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_SalesOrderEvents_Shape",
                table: "SalesOrderEvents");

            migrationBuilder.DropColumn(
                name: "Changes",
                table: "SalesOrderEvents");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SalesOrderEvents_Shape",
                table: "SalesOrderEvents",
                sql: "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND isfinite(\"RecordedAtUtc\") AND \"Sequence\" > 0 AND \"Kind\" IN ('Created', 'Revised', 'Submitted', 'Approved', 'Rejected', 'Cancelled', 'Reassigned') AND \"ToStatus\" IN ('Draft', 'PendingManagerApproval', 'Approved', 'Cancelled') AND (\"FromStatus\" IS NULL OR \"FromStatus\" IN ('Draft', 'PendingManagerApproval', 'Approved', 'Cancelled')) AND ((\"Kind\" IN ('Rejected', 'Cancelled') AND \"Reason\" IS NOT NULL AND \"Reason\" = btrim(\"Reason\") AND length(\"Reason\") BETWEEN 1 AND 1000 AND \"Reason\" !~ '[[:cntrl:]]') OR (\"Kind\" NOT IN ('Rejected', 'Cancelled') AND \"Reason\" IS NULL))");
        }
    }
}

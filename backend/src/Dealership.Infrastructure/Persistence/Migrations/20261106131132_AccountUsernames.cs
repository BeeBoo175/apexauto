using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dealership.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AccountUsernames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AccountUsername",
                table: "AspNetUsers",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            // Existing accounts start from the email's local part, with a numeric suffix when two would clash.
            migrationBuilder.Sql("""
                DO $$
                DECLARE
                    account record;
                    candidate text;
                    suffix integer;
                BEGIN
                    FOR account IN
                        SELECT "Id",
                            CASE WHEN length(cleaned) < 3 THEN rpad(cleaned, 3, '0') ELSE left(cleaned, 30) END AS stem
                        FROM (
                            SELECT "Id", regexp_replace(split_part("Email", '@', 1), '[^A-Za-z0-9._]', '', 'g') AS cleaned
                            FROM "AspNetUsers"
                        ) AS candidates
                        ORDER BY "Id"
                    LOOP
                        candidate := account.stem;
                        suffix := 1;

                        WHILE EXISTS (SELECT 1 FROM "AspNetUsers" WHERE upper("AccountUsername") = upper(candidate)) LOOP
                            suffix := suffix + 1;
                            candidate := left(account.stem, 30 - length(suffix::text)) || suffix::text;
                        END LOOP;

                        UPDATE "AspNetUsers" SET "AccountUsername" = candidate WHERE "Id" = account."Id";
                    END LOOP;
                END $$;
                """);
            migrationBuilder.Sql("ALTER TABLE \"AspNetUsers\" ALTER COLUMN \"AccountUsername\" DROP DEFAULT;");

            migrationBuilder.AddColumn<string>(
                name: "NormalizedAccountUsername",
                table: "AspNetUsers",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true,
                computedColumnSql: "upper(\"AccountUsername\")",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "AccountUsernameIndex",
                table: "AspNetUsers",
                column: "NormalizedAccountUsername",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_AccountUsername",
                table: "AspNetUsers",
                sql: "\"AccountUsername\" ~ '^[A-Za-z0-9._]{3,30}$'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "AccountUsernameIndex",
                table: "AspNetUsers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_AccountUsername",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "NormalizedAccountUsername",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "AccountUsername",
                table: "AspNetUsers");
        }
    }
}

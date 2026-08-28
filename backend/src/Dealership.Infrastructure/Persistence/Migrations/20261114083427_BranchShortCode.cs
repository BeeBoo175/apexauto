using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dealership.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BranchShortCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ShortCode",
                table: "Branches",
                type: "character varying(4)",
                maxLength: 4,
                nullable: false,
                defaultValue: "");

            // Existing branches get a code from their name: the first three consonants of a one-word place, else the initials.
            migrationBuilder.Sql("""
                UPDATE "Branches" SET "ShortCode" = (
                    SELECT CASE WHEN count(*) = 1 THEN left(regexp_replace(upper(min(word)), '[AEIOU]', '', 'g'), 3)
                                ELSE left(upper(string_agg(left(word, 1), '' ORDER BY position)), 4) END
                    FROM regexp_split_to_table(regexp_replace("Branches"."Name", '^ApexAuto\s+', ''), '\s+')
                        WITH ORDINALITY AS words(word, position));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ShortCode",
                table: "Branches");
        }
    }
}

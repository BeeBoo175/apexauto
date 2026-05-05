using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dealership.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReservationNumbers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Number",
                table: "Reservations",
                type: "character varying(15)",
                maxLength: 15,
                nullable: true,
                collation: "C");

            migrationBuilder.CreateTable(
                name: "ReservationNumberCounters",
                columns: table => new
                {
                    Year = table.Column<int>(type: "integer", nullable: false),
                    LastValue = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReservationNumberCounters", x => x.Year);
                    table.CheckConstraint("CK_ReservationNumberCounters_Shape", "\"Year\" BETWEEN 2000 AND 9999 AND \"LastValue\" BETWEEN 0 AND 999999");
                });

            // Existing holds are numbered once, per UTC creation year in creation order, and the counters resume after them.
            // The retained-confirmation guard forbids updates, so it is suspended for this maintenance statement only.
            migrationBuilder.Sql("""
                ALTER TABLE "Reservations" DISABLE TRIGGER USER;
                WITH numbered AS (
                    SELECT "Id",
                        EXTRACT(YEAR FROM "CreatedAt" AT TIME ZONE 'UTC')::int AS "Year",
                        row_number() OVER (
                            PARTITION BY EXTRACT(YEAR FROM "CreatedAt" AT TIME ZONE 'UTC')
                            ORDER BY "CreatedAt", "Id") AS "Position"
                    FROM "Reservations")
                UPDATE "Reservations" AS reservation
                SET "Number" = 'VRS-' || lpad(numbered."Year"::text, 4, '0') || '-' || lpad(numbered."Position"::text, 6, '0')
                FROM numbered
                WHERE reservation."Id" = numbered."Id";
                ALTER TABLE "Reservations" ENABLE TRIGGER USER;
                INSERT INTO "ReservationNumberCounters" ("Year", "LastValue")
                SELECT EXTRACT(YEAR FROM "CreatedAt" AT TIME ZONE 'UTC')::int, count(*)
                FROM "Reservations" GROUP BY 1;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "Number",
                table: "Reservations",
                type: "character varying(15)",
                maxLength: 15,
                nullable: false,
                defaultValue: "",
                collation: "C",
                oldClrType: typeof(string),
                oldType: "character varying(15)",
                oldMaxLength: 15,
                oldNullable: true,
                oldCollation: "C");

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_Number",
                table: "Reservations",
                column: "Number",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Reservations_Number",
                table: "Reservations",
                sql: "\"Number\" ~ '^VRS-[0-9]{4}-[0-9]{6}$'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReservationNumberCounters");

            migrationBuilder.DropIndex(
                name: "IX_Reservations_Number",
                table: "Reservations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Reservations_Number",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "Number",
                table: "Reservations");
        }
    }
}

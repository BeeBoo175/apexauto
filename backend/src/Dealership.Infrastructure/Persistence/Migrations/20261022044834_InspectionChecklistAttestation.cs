using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dealership.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InspectionChecklistAttestation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExaminedPointCodesJson",
                table: "InspectionDraftSaves",
                type: "jsonb",
                nullable: true);

            migrationBuilder.Sql("""
                CREATE FUNCTION valid_inspection_attestation(entries jsonb, examined jsonb) RETURNS boolean
                LANGUAGE plpgsql IMMUTABLE AS $$
                DECLARE
                    required text[];
                    observed text[];
                BEGIN
                    IF NOT valid_inspection_draft_entries(entries) OR
                       jsonb_typeof(examined) IS DISTINCT FROM 'array' THEN
                        RETURN false;
                    END IF;
                    IF jsonb_array_length(examined) > 210 OR octet_length(examined::text) > 4096 OR
                       EXISTS (SELECT 1 FROM jsonb_array_elements(examined) item
                           WHERE jsonb_typeof(item) IS DISTINCT FROM 'string') THEN
                        RETURN false;
                    END IF;
                    SELECT coalesce(array_agg(code ORDER BY code COLLATE "C"), ARRAY[]::text[]) INTO required
                    FROM (
                        SELECT prefix || '-' || lpad(number::text, 3, '0') AS code
                        FROM (VALUES ('FC', 45), ('ET', 50), ('EP', 45), ('IE', 40), ('UB', 30)) AS catalog(prefix, points)
                        CROSS JOIN LATERAL generate_series(1, points) AS number
                    ) catalog
                    WHERE NOT EXISTS (SELECT 1 FROM jsonb_array_elements(entries) entry
                        WHERE entry->>'kind' = '1' AND entry->>'pointCode' = catalog.code);
                    SELECT coalesce(array_agg(code ORDER BY code COLLATE "C"), ARRAY[]::text[]) INTO observed
                    FROM jsonb_array_elements_text(examined) AS code;
                    RETURN observed = required;
                END $$;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_InspectionDraftSaves_Attestation",
                table: "InspectionDraftSaves",
                sql: "\"ExaminedPointCodesJson\" IS NULL OR valid_inspection_attestation(\"EntriesJson\", \"ExaminedPointCodesJson\")");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_InspectionDraftSaves_Attestation",
                table: "InspectionDraftSaves");

            migrationBuilder.DropColumn(
                name: "ExaminedPointCodesJson",
                table: "InspectionDraftSaves");

            migrationBuilder.Sql("DROP FUNCTION valid_inspection_attestation(jsonb, jsonb);");
        }
    }
}

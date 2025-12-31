using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dealership.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class IntakePhotos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IntakePhotos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Angle = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ObjectKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Bytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Width = table.Column<int>(type: "integer", nullable: false),
                    Height = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AvailableAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntakePhotos", x => x.Id);
                    table.CheckConstraint("CK_IntakePhotos_Content", "\"Angle\" IN ('Driver', 'Passenger', 'Front', 'Rear') AND \"ContentType\" IN ('image/jpeg', 'image/png', 'image/webp') AND \"Bytes\" BETWEEN 1 AND 10485760 AND \"Sha256\" ~ '^[0-9a-f]{64}$' AND \"Width\" > 0 AND \"Height\" > 0");
                    table.CheckConstraint("CK_IntakePhotos_Identity", "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"ObjectKey\" = 'intake/photos/' || replace(\"Id\"::text, '-', '')");
                    table.CheckConstraint("CK_IntakePhotos_Lifetime", "\"ExpiresAt\" > \"CreatedAt\" AND (\"AvailableAt\" IS NULL OR (\"AvailableAt\" >= \"CreatedAt\" AND \"AvailableAt\" < \"ExpiresAt\"))");
                    table.ForeignKey(
                        name: "FK_IntakePhotos_CustomerProfiles_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "CustomerProfiles",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IntakePhotos_ExpiresAt",
                table: "IntakePhotos",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_IntakePhotos_OwnerId_CreatedAt",
                table: "IntakePhotos",
                columns: ["OwnerId", "CreatedAt"]);

            migrationBuilder.Sql("""
                CREATE FUNCTION public.protect_intake_photo() RETURNS trigger
                LANGUAGE plpgsql SET search_path = pg_catalog AS $$
                BEGIN
                    IF (NEW."Id", NEW."OwnerId", NEW."Angle", NEW."ObjectKey", NEW."ContentType",
                        NEW."Bytes", NEW."Sha256", NEW."Width", NEW."Height", NEW."CreatedAt", NEW."ExpiresAt")
                        IS DISTINCT FROM
                        (OLD."Id", OLD."OwnerId", OLD."Angle", OLD."ObjectKey", OLD."ContentType",
                        OLD."Bytes", OLD."Sha256", OLD."Width", OLD."Height", OLD."CreatedAt", OLD."ExpiresAt")
                        OR (OLD."AvailableAt" IS NOT NULL AND NEW."AvailableAt" IS DISTINCT FROM OLD."AvailableAt") THEN
                        RAISE EXCEPTION 'Photo identity, contents, lifetime and confirmed receipts are immutable.'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_IntakePhotos_Immutable';
                    END IF;
                    RETURN NEW;
                END;
                $$;
                CREATE TRIGGER protect_intake_photo BEFORE UPDATE ON public."IntakePhotos"
                    FOR EACH ROW EXECUTE FUNCTION public.protect_intake_photo();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IntakePhotos");

            migrationBuilder.Sql("DROP FUNCTION public.protect_intake_photo();");
        }
    }
}

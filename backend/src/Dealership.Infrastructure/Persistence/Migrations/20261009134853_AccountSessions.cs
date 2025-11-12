using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dealership.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AccountSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccountSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SecurityStamp = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountSessions", x => x.Id);
                    table.CheckConstraint("CK_AccountSessions_Identity", "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid");
                    table.CheckConstraint("CK_AccountSessions_Lifetime", "\"ExpiresAt\" > \"CreatedAt\" AND (\"RevokedAt\" IS NULL OR \"RevokedAt\" >= \"CreatedAt\")");
                    table.CheckConstraint("CK_AccountSessions_Stamp", "\"SecurityStamp\" ~ '[^[:space:]]'");
                    table.ForeignKey(
                        name: "FK_AccountSessions_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountSessions_UserId",
                table: "AccountSessions",
                column: "UserId");

            migrationBuilder.Sql("""
                CREATE FUNCTION public.protect_account_session() RETURNS trigger
                LANGUAGE plpgsql SET search_path = pg_catalog AS $$
                BEGIN
                    IF (NEW."Id", NEW."UserId", NEW."SecurityStamp", NEW."CreatedAt", NEW."ExpiresAt")
                        IS DISTINCT FROM
                        (OLD."Id", OLD."UserId", OLD."SecurityStamp", OLD."CreatedAt", OLD."ExpiresAt")
                        OR (OLD."RevokedAt" IS NOT NULL AND NEW."RevokedAt" IS DISTINCT FROM OLD."RevokedAt") THEN
                        RAISE EXCEPTION 'Session identity, lifetime and completed revocation are immutable.'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_AccountSessions_Immutable';
                    END IF;
                    RETURN NEW;
                END;
                $$;
                CREATE TRIGGER protect_account_session BEFORE UPDATE ON public."AccountSessions"
                    FOR EACH ROW EXECUTE FUNCTION public.protect_account_session();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountSessions");

            migrationBuilder.Sql("DROP FUNCTION public.protect_account_session();");
        }
    }
}

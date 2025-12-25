using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dealership.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PasswordRecovery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PasswordResetChallenges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientEmail = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    InvalidatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConsumedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedSecurityStamp = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    DeliveryAttempts = table.Column<int>(type: "integer", nullable: false),
                    NextDeliveryAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeliveryClaimId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeliveryClaimExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeliveredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PasswordResetChallenges", x => x.Id);
                    table.CheckConstraint("CK_PasswordResetChallenges_Attempts", "\"DeliveryAttempts\" >= 0");
                    table.CheckConstraint("CK_PasswordResetChallenges_Identity", "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"RecipientEmail\" ~ '[^[:space:]]'");
                    table.CheckConstraint("CK_PasswordResetChallenges_Lifetime", "\"ExpiresAt\" = \"CreatedAt\" + interval '1 hour'");
                    table.CheckConstraint("CK_PasswordResetChallenges_Receipt", "(\"ConsumedAt\" IS NULL AND \"CompletedSecurityStamp\" IS NULL) OR\n(\"ConsumedAt\" IS NOT NULL AND \"CompletedSecurityStamp\" IS NOT NULL AND\n length(\"CompletedSecurityStamp\") > 0 AND \"ConsumedAt\" >= \"CreatedAt\" AND\n \"ConsumedAt\" < \"ExpiresAt\" AND \"InvalidatedAt\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_PasswordResetChallenges_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetChallenges_NextDeliveryAt",
                table: "PasswordResetChallenges",
                column: "NextDeliveryAt");

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetChallenges_UserId",
                table: "PasswordResetChallenges",
                column: "UserId",
                unique: true,
                filter: "\"InvalidatedAt\" IS NULL AND \"ConsumedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetChallenges_UserId_CreatedAt",
                table: "PasswordResetChallenges",
                columns: ["UserId", "CreatedAt"]);

            migrationBuilder.Sql("""
                CREATE FUNCTION public.protect_password_reset() RETURNS trigger
                LANGUAGE plpgsql SET search_path = pg_catalog AS $$
                BEGIN
                    IF (NEW."Id", NEW."UserId", NEW."RecipientEmail", NEW."CreatedAt", NEW."ExpiresAt")
                        IS DISTINCT FROM
                        (OLD."Id", OLD."UserId", OLD."RecipientEmail", OLD."CreatedAt", OLD."ExpiresAt")
                        OR (OLD."ConsumedAt" IS NOT NULL AND
                            (NEW."ConsumedAt", NEW."CompletedSecurityStamp") IS DISTINCT FROM
                            (OLD."ConsumedAt", OLD."CompletedSecurityStamp"))
                        OR (OLD."InvalidatedAt" IS NOT NULL AND
                            NEW."InvalidatedAt" IS DISTINCT FROM OLD."InvalidatedAt") THEN
                        RAISE EXCEPTION 'Reset identity, lifetime and completed receipts are immutable.'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_PasswordResetChallenges_Immutable';
                    END IF;
                    RETURN NEW;
                END;
                $$;
                CREATE TRIGGER protect_password_reset BEFORE UPDATE ON public."PasswordResetChallenges"
                    FOR EACH ROW EXECUTE FUNCTION public.protect_password_reset();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PasswordResetChallenges");

            migrationBuilder.Sql("DROP FUNCTION public.protect_password_reset();");
        }
    }
}

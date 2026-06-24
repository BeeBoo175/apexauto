using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dealership.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StaffProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StaffProfiles",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Mobile = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffProfiles", x => x.UserId);
                    table.CheckConstraint("CK_StaffProfiles_RequiredText", "\"Name\" ~ '[^[:space:]]' AND \"Mobile\" ~ '[^[:space:]]'");
                    table.ForeignKey(
                        name: "FK_StaffProfiles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Existing staff receive an email-derived name and a placeholder work number until an Administrator edits them.
            migrationBuilder.Sql("""
                INSERT INTO "StaffProfiles" ("UserId", "Name", "Mobile")
                SELECT "Id",
                    COALESCE(NULLIF(btrim(initcap(replace(replace(split_part("Email", '@', 1), '.', ' '), '_', ' '))), ''), 'Staff member'),
                    'Not provided'
                FROM "AspNetUsers"
                WHERE "Role" <> 'Customer';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StaffProfiles");
        }
    }
}

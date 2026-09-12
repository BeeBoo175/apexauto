using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dealership.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StaffErasure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_StaffProfiles_RequiredText",
                table: "StaffProfiles");

            migrationBuilder.DropCheckConstraint(
                name: "CK_StaffInvitations_Profile",
                table: "StaffInvitations");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "StaffProfiles",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "Mobile",
                table: "StaffProfiles",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ErasedAt",
                table: "StaffProfiles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "StaffInvitations",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "Mobile",
                table: "StaffInvitations",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30);

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "StaffInvitations",
                type: "character varying(254)",
                maxLength: 254,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(254)",
                oldMaxLength: 254);

            migrationBuilder.AddCheckConstraint(
                name: "CK_StaffProfiles_RequiredText",
                table: "StaffProfiles",
                sql: "(\"ErasedAt\" IS NULL AND \"Name\" IS NOT NULL AND \"Mobile\" IS NOT NULL AND \"Name\" ~ '[^[:space:]]' AND \"Mobile\" ~ '[^[:space:]]') OR (\"ErasedAt\" IS NOT NULL AND \"Name\" IS NULL AND \"Mobile\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_StaffInvitations_Profile",
                table: "StaffInvitations",
                sql: "(\"Email\" IS NOT NULL AND \"Name\" IS NOT NULL AND \"Mobile\" IS NOT NULL AND \"Name\" ~ '[^[:space:]]' AND \"Mobile\" ~ '[^[:space:]]') OR (\"ActivatedAt\" IS NOT NULL AND \"Email\" IS NULL AND \"Name\" IS NULL AND \"Mobile\" IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM public."StaffProfiles" WHERE "ErasedAt" IS NOT NULL)
                        OR EXISTS (SELECT 1 FROM public."StaffInvitations" WHERE "Email" IS NULL) THEN
                        RAISE EXCEPTION 'Cannot revert StaffErasure while erased employees exist.';
                    END IF;
                END
                $$;
                """);

            migrationBuilder.DropCheckConstraint(
                name: "CK_StaffProfiles_RequiredText",
                table: "StaffProfiles");

            migrationBuilder.DropCheckConstraint(
                name: "CK_StaffInvitations_Profile",
                table: "StaffInvitations");

            migrationBuilder.DropColumn(
                name: "ErasedAt",
                table: "StaffProfiles");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "StaffProfiles",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Mobile",
                table: "StaffProfiles",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "StaffInvitations",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Mobile",
                table: "StaffInvitations",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "StaffInvitations",
                type: "character varying(254)",
                maxLength: 254,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(254)",
                oldMaxLength: 254,
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_StaffProfiles_RequiredText",
                table: "StaffProfiles",
                sql: "\"Name\" ~ '[^[:space:]]' AND \"Mobile\" ~ '[^[:space:]]'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_StaffInvitations_Profile",
                table: "StaffInvitations",
                sql: "\"Name\" ~ '[^[:space:]]' AND \"Mobile\" ~ '[^[:space:]]'");
        }
    }
}

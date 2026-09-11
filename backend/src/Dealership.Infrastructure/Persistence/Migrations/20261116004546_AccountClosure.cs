using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dealership.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AccountClosure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_CustomerProfiles_Nric",
                table: "CustomerProfiles");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CustomerProfiles_RequiredText",
                table: "CustomerProfiles");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_AccountUsername",
                table: "AspNetUsers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_EmailNormalization",
                table: "AspNetUsers");

            migrationBuilder.AlterColumn<string>(
                name: "ResidentialAddress",
                table: "CustomerProfiles",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000);

            migrationBuilder.AlterColumn<string>(
                name: "Nric",
                table: "CustomerProfiles",
                type: "character varying(12)",
                maxLength: 12,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(12)",
                oldMaxLength: 12);

            migrationBuilder.AlterColumn<string>(
                name: "Mobile",
                table: "CustomerProfiles",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30);

            migrationBuilder.AlterColumn<string>(
                name: "LegalName",
                table: "CustomerProfiles",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ErasedAt",
                table: "CustomerProfiles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "NormalizedEmail",
                table: "AspNetUsers",
                type: "character varying(254)",
                maxLength: 254,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(254)",
                oldMaxLength: 254);

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "AspNetUsers",
                type: "character varying(254)",
                maxLength: 254,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(254)",
                oldMaxLength: 254);

            migrationBuilder.AlterColumn<string>(
                name: "AccountUsername",
                table: "AspNetUsers",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ClosedAt",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_CustomerProfiles_Nric",
                table: "CustomerProfiles",
                sql: "(\"ErasedAt\" IS NULL AND \"Nric\" IS NOT NULL AND \"Nric\" ~ '^[0-9]{12}$') OR (\"ErasedAt\" IS NOT NULL AND \"Nric\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CustomerProfiles_RequiredText",
                table: "CustomerProfiles",
                sql: "(\"ErasedAt\" IS NULL AND \"LegalName\" IS NOT NULL AND \"Mobile\" IS NOT NULL AND \"ResidentialAddress\" IS NOT NULL AND \"LegalName\" ~ '[^[:space:]]' AND \"Mobile\" ~ '[^[:space:]]' AND \"ResidentialAddress\" ~ '[^[:space:]]') OR (\"ErasedAt\" IS NOT NULL AND \"LegalName\" IS NULL AND \"Mobile\" IS NULL AND \"ResidentialAddress\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_AccountUsername",
                table: "AspNetUsers",
                sql: "(\"ClosedAt\" IS NULL AND \"AccountUsername\" IS NOT NULL AND \"AccountUsername\" ~ '^[A-Za-z0-9._]{3,30}$') OR (\"ClosedAt\" IS NOT NULL AND \"AccountUsername\" IS NULL AND \"UserName\" IS NULL AND \"NormalizedUserName\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_Closed",
                table: "AspNetUsers",
                sql: "\"ClosedAt\" IS NULL OR (NOT \"IsActive\" AND \"SuspendedAt\" IS NULL AND \"PasswordHash\" IS NULL AND NOT \"EmailConfirmed\")");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_EmailNormalization",
                table: "AspNetUsers",
                sql: "(\"ClosedAt\" IS NULL AND \"Email\" IS NOT NULL AND \"NormalizedEmail\" IS NOT NULL AND length(\"Email\") > 0 AND \"Email\" = btrim(\"Email\", U&'\\0009\\000A\\000B\\000C\\000D\\0020\\0085\\00A0\\1680\\2000\\2001\\2002\\2003\\2004\\2005\\2006\\2007\\2008\\2009\\200A\\2028\\2029\\202F\\205F\\3000') AND \"NormalizedEmail\" = upper(\"Email\")) OR (\"ClosedAt\" IS NOT NULL AND \"Email\" IS NULL AND \"NormalizedEmail\" IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restoring NOT NULL would need invented personal values for erased rows, so refuse instead.
            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM ""AspNetUsers"" WHERE ""ClosedAt"" IS NOT NULL)
        OR EXISTS (SELECT 1 FROM ""CustomerProfiles"" WHERE ""ErasedAt"" IS NOT NULL) THEN
        RAISE EXCEPTION 'Cannot revert AccountClosure while closed or erased accounts exist.';
    END IF;
END $$;");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CustomerProfiles_Nric",
                table: "CustomerProfiles");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CustomerProfiles_RequiredText",
                table: "CustomerProfiles");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_AccountUsername",
                table: "AspNetUsers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_Closed",
                table: "AspNetUsers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_EmailNormalization",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "ErasedAt",
                table: "CustomerProfiles");

            migrationBuilder.DropColumn(
                name: "ClosedAt",
                table: "AspNetUsers");

            migrationBuilder.AlterColumn<string>(
                name: "ResidentialAddress",
                table: "CustomerProfiles",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Nric",
                table: "CustomerProfiles",
                type: "character varying(12)",
                maxLength: 12,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(12)",
                oldMaxLength: 12,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Mobile",
                table: "CustomerProfiles",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "LegalName",
                table: "CustomerProfiles",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "NormalizedEmail",
                table: "AspNetUsers",
                type: "character varying(254)",
                maxLength: 254,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(254)",
                oldMaxLength: 254,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "AspNetUsers",
                type: "character varying(254)",
                maxLength: 254,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(254)",
                oldMaxLength: 254,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "AccountUsername",
                table: "AspNetUsers",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30,
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_CustomerProfiles_Nric",
                table: "CustomerProfiles",
                sql: "\"Nric\" ~ '^[0-9]{12}$'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CustomerProfiles_RequiredText",
                table: "CustomerProfiles",
                sql: "\"LegalName\" ~ '[^[:space:]]' AND \"Mobile\" ~ '[^[:space:]]' AND \"ResidentialAddress\" ~ '[^[:space:]]'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_AccountUsername",
                table: "AspNetUsers",
                sql: "\"AccountUsername\" ~ '^[A-Za-z0-9._]{3,30}$'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_EmailNormalization",
                table: "AspNetUsers",
                sql: "length(\"Email\") > 0 AND \"Email\" = btrim(\"Email\", U&'\\0009\\000A\\000B\\000C\\000D\\0020\\0085\\00A0\\1680\\2000\\2001\\2002\\2003\\2004\\2005\\2006\\2007\\2008\\2009\\200A\\2028\\2029\\202F\\205F\\3000') AND \"NormalizedEmail\" = upper(\"Email\")");
        }
    }
}

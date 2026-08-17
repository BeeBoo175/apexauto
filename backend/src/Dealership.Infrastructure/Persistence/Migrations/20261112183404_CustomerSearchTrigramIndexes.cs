using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dealership.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CustomerSearchTrigramIndexes : Migration
    {
        private static readonly string[] TrigramOperators = ["gin_trgm_ops"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerProfiles_LegalName_Trigram",
                table: "CustomerProfiles",
                column: "LegalName")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", TrigramOperators);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerProfiles_Mobile_Trigram",
                table: "CustomerProfiles",
                column: "Mobile")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", TrigramOperators);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_Email_Trigram",
                table: "AspNetUsers",
                column: "Email")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", TrigramOperators);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CustomerProfiles_LegalName_Trigram",
                table: "CustomerProfiles");

            migrationBuilder.DropIndex(
                name: "IX_CustomerProfiles_Mobile_Trigram",
                table: "CustomerProfiles");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_Email_Trigram",
                table: "AspNetUsers");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:pg_trgm", ",,");
        }
    }
}

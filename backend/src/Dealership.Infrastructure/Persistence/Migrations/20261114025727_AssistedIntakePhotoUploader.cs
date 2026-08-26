using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dealership.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AssistedIntakePhotoUploader : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "UploadedByStaffId",
                table: "IntakePhotos",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntakePhotos_UploadedByStaffId",
                table: "IntakePhotos",
                column: "UploadedByStaffId");

            migrationBuilder.AddForeignKey(
                name: "FK_IntakePhotos_AspNetUsers_UploadedByStaffId",
                table: "IntakePhotos",
                column: "UploadedByStaffId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_IntakePhotos_AspNetUsers_UploadedByStaffId",
                table: "IntakePhotos");

            migrationBuilder.DropIndex(
                name: "IX_IntakePhotos_UploadedByStaffId",
                table: "IntakePhotos");

            migrationBuilder.DropColumn(
                name: "UploadedByStaffId",
                table: "IntakePhotos");
        }
    }
}

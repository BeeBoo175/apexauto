using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dealership.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StaffAssistedBookings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BookedByStaffId",
                table: "TestDriveBookings",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BookedByStaffId",
                table: "Reservations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BookedByStaffId",
                table: "IntakeAppointments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TestDriveBookings_BookedByStaffId",
                table: "TestDriveBookings",
                column: "BookedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_BookedByStaffId",
                table: "Reservations",
                column: "BookedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_IntakeAppointments_BookedByStaffId",
                table: "IntakeAppointments",
                column: "BookedByStaffId");

            migrationBuilder.AddForeignKey(
                name: "FK_IntakeAppointments_AspNetUsers_BookedByStaffId",
                table: "IntakeAppointments",
                column: "BookedByStaffId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Reservations_AspNetUsers_BookedByStaffId",
                table: "Reservations",
                column: "BookedByStaffId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TestDriveBookings_AspNetUsers_BookedByStaffId",
                table: "TestDriveBookings",
                column: "BookedByStaffId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_IntakeAppointments_AspNetUsers_BookedByStaffId",
                table: "IntakeAppointments");

            migrationBuilder.DropForeignKey(
                name: "FK_Reservations_AspNetUsers_BookedByStaffId",
                table: "Reservations");

            migrationBuilder.DropForeignKey(
                name: "FK_TestDriveBookings_AspNetUsers_BookedByStaffId",
                table: "TestDriveBookings");

            migrationBuilder.DropIndex(
                name: "IX_TestDriveBookings_BookedByStaffId",
                table: "TestDriveBookings");

            migrationBuilder.DropIndex(
                name: "IX_Reservations_BookedByStaffId",
                table: "Reservations");

            migrationBuilder.DropIndex(
                name: "IX_IntakeAppointments_BookedByStaffId",
                table: "IntakeAppointments");

            migrationBuilder.DropColumn(
                name: "BookedByStaffId",
                table: "TestDriveBookings");

            migrationBuilder.DropColumn(
                name: "BookedByStaffId",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "BookedByStaffId",
                table: "IntakeAppointments");
        }
    }
}

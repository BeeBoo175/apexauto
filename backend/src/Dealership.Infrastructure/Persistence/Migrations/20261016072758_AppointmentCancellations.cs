using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dealership.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AppointmentCancellations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_IntakeAppointments_BranchId_StartsAt",
                table: "IntakeAppointments");

            migrationBuilder.CreateTable(
                name: "AppointmentCancellations",
                columns: table => new
                {
                    AppointmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    CancelledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppointmentCancellations", x => x.AppointmentId);
                    table.ForeignKey(
                        name: "FK_AppointmentCancellations_IntakeAppointments_AppointmentId_O~",
                        columns: x => new { x.AppointmentId, x.OwnerId },
                        principalTable: "IntakeAppointments",
                        principalColumns: ["Id", "OwnerId"],
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppointmentCancellationMails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    DeliveryAttempts = table.Column<int>(type: "integer", nullable: false),
                    NextDeliveryAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeliveryClaimId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeliveryClaimExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeliveredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppointmentCancellationMails", x => x.Id);
                    table.CheckConstraint("CK_AppointmentCancellationMails_Shape", "\"RecipientEmail\" ~ '[^[:space:]]' AND \"DeliveryAttempts\" >= 0");
                    table.ForeignKey(
                        name: "FK_AppointmentCancellationMails_AppointmentCancellations_Id",
                        column: x => x.Id,
                        principalTable: "AppointmentCancellations",
                        principalColumn: "AppointmentId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IntakeAppointments_BranchId_StartsAt",
                table: "IntakeAppointments",
                columns: ["BranchId", "StartsAt"]);

            migrationBuilder.CreateIndex(
                name: "IX_IntakeAppointments_OwnerId_StartsAt_Id",
                table: "IntakeAppointments",
                columns: ["OwnerId", "StartsAt", "Id"]);

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentCancellationMails_NextDeliveryAt_Id",
                table: "AppointmentCancellationMails",
                columns: ["NextDeliveryAt", "Id"]);

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentCancellations_AppointmentId_OwnerId",
                table: "AppointmentCancellations",
                columns: ["AppointmentId", "OwnerId"],
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentCancellations_CancelledAt",
                table: "AppointmentCancellations",
                column: "CancelledAt");

            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION public.protect_appointment_slot() RETURNS trigger
                LANGUAGE plpgsql SET search_path = pg_catalog AS $$
                BEGIN
                    -- Advance the row version so stale repeatable-read writers fail instead of missing allocations.
                    UPDATE public."Branches" SET "IsActive" = "IsActive" WHERE "Id" = NEW."BranchId";
                    IF EXISTS (SELECT 1 FROM public."IntakeAppointments" a
                        WHERE a."BranchId" = NEW."BranchId" AND a."StartsAt" = NEW."StartsAt"
                          AND NOT EXISTS (SELECT 1 FROM public."AppointmentCancellations" c WHERE c."AppointmentId" = a."Id")) THEN
                        RAISE EXCEPTION 'The branch slot is occupied.'
                            USING ERRCODE = '23505', CONSTRAINT = 'IX_IntakeAppointments_BranchId_StartsAt';
                    END IF;
                    IF EXISTS (SELECT 1 FROM public."IntakeAppointments" a
                        WHERE a."BranchId" = NEW."BranchId" AND a."StartsAt" < NEW."EndsAt" AND NEW."StartsAt" < a."EndsAt"
                          AND NOT EXISTS (SELECT 1 FROM public."AppointmentCancellations" c WHERE c."AppointmentId" = a."Id")) THEN
                        RAISE EXCEPTION 'The branch already has an overlapping appointment.'
                            USING ERRCODE = '23P01', CONSTRAINT = 'EX_IntakeAppointments_Slot';
                    END IF;
                    RETURN NEW;
                END;
                $$;

                CREATE FUNCTION public.validate_appointment_cancellation() RETURNS trigger
                LANGUAGE plpgsql SET search_path = pg_catalog AS $$
                DECLARE appointment public."IntakeAppointments"%ROWTYPE;
                BEGIN
                    SELECT * INTO appointment FROM public."IntakeAppointments" WHERE "Id" = NEW."AppointmentId";
                    IF FOUND THEN
                        PERFORM 1 FROM public."Branches" WHERE "Id" = appointment."BranchId" FOR UPDATE;
                        IF NEW."CancelledAt" < appointment."CreatedAt" OR NEW."CancelledAt" >= appointment."StartsAt"
                            OR clock_timestamp() >= appointment."StartsAt" THEN
                            RAISE EXCEPTION 'Cancellation must precede appointment start.'
                                USING ERRCODE = '23514', CONSTRAINT = 'CK_AppointmentCancellations_Time';
                        END IF;
                    END IF;
                    RETURN NEW;
                END;
                $$;
                CREATE TRIGGER validate_appointment_cancellation BEFORE INSERT ON public."AppointmentCancellations"
                    FOR EACH ROW EXECUTE FUNCTION public.validate_appointment_cancellation();
                CREATE TRIGGER protect_appointment_cancellation BEFORE UPDATE OR DELETE ON public."AppointmentCancellations"
                    FOR EACH ROW EXECUTE FUNCTION public.protect_appointment_evidence();
                CREATE TRIGGER protect_cancellation_mail BEFORE UPDATE ON public."AppointmentCancellationMails"
                    FOR EACH ROW EXECUTE FUNCTION public.protect_appointment_confirmation();

                CREATE FUNCTION public.require_cancellation_mail() RETURNS trigger
                LANGUAGE plpgsql SET search_path = pg_catalog AS $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM public."AppointmentCancellationMails" WHERE "Id" = NEW."AppointmentId") THEN
                        RAISE EXCEPTION 'Cancellation requires durable mail intent.'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_AppointmentCancellations_Mail';
                    END IF;
                    RETURN NULL;
                END;
                $$;
                CREATE CONSTRAINT TRIGGER require_cancellation_mail AFTER INSERT ON public."AppointmentCancellations"
                    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION public.require_cancellation_mail();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP FUNCTION public.validate_appointment_cancellation() CASCADE;
                DROP FUNCTION public.require_cancellation_mail() CASCADE;
                CREATE OR REPLACE FUNCTION public.protect_appointment_slot() RETURNS trigger
                LANGUAGE plpgsql SET search_path = pg_catalog AS $$
                BEGIN
                    PERFORM 1 FROM public."Branches" WHERE "Id" = NEW."BranchId" FOR UPDATE;
                    IF EXISTS (SELECT 1 FROM public."IntakeAppointments"
                        WHERE "BranchId" = NEW."BranchId" AND "StartsAt" <> NEW."StartsAt"
                          AND "StartsAt" < NEW."EndsAt" AND NEW."StartsAt" < "EndsAt") THEN
                        RAISE EXCEPTION 'The branch already has an overlapping appointment.'
                            USING ERRCODE = '23P01', CONSTRAINT = 'EX_IntakeAppointments_Slot';
                    END IF;
                    RETURN NEW;
                END;
                $$;
                """);
            migrationBuilder.DropTable(
                name: "AppointmentCancellationMails");

            migrationBuilder.DropTable(
                name: "AppointmentCancellations");

            migrationBuilder.DropIndex(
                name: "IX_IntakeAppointments_BranchId_StartsAt",
                table: "IntakeAppointments");

            migrationBuilder.DropIndex(
                name: "IX_IntakeAppointments_OwnerId_StartsAt_Id",
                table: "IntakeAppointments");

            migrationBuilder.CreateIndex(
                name: "IX_IntakeAppointments_BranchId_StartsAt",
                table: "IntakeAppointments",
                columns: ["BranchId", "StartsAt"],
                unique: true);
        }
    }
}

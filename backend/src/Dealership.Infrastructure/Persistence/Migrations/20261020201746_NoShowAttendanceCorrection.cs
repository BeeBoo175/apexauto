using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dealership.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NoShowAttendanceCorrection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CorrectionReason",
                table: "AppointmentCheckIns",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CorrectionRecordedAt",
                table: "AppointmentCheckIns",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_AppointmentCheckIns_Correction",
                table: "AppointmentCheckIns",
                sql: "(\"CorrectionRecordedAt\" IS NULL AND \"CorrectionReason\" IS NULL) OR (\"CorrectionRecordedAt\" IS NOT NULL AND \"CorrectionReason\" IS NOT NULL AND \"CorrectionReason\" ~ '[^[:space:]]' AND \"CorrectionReason\" !~ '[[:cntrl:]]')");
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION public.validate_appointment_checkin() RETURNS trigger
                LANGUAGE plpgsql SET search_path = pg_catalog AS $$
                DECLARE
                    appointment public."IntakeAppointments"%ROWTYPE;
                    photo_id uuid;
                BEGIN
                    SELECT * INTO appointment FROM public."IntakeAppointments" WHERE "Id" = NEW."AppointmentId";
                    IF NOT FOUND THEN
                        RETURN NEW;
                    END IF;
                    PERFORM 1 FROM public."AspNetUsers" WHERE "Id" IN (NEW."StaffId", appointment."OwnerId")
                        ORDER BY "Id" FOR SHARE;
                    -- Advance the shared branch version to fence stale cancellation/check-in snapshots.
                    UPDATE public."Branches" SET "IsActive" = "IsActive" WHERE "Id" = appointment."BranchId";
                    IF NOT EXISTS (SELECT 1 FROM public."AspNetUsers" u JOIN public."Branches" b ON b."Id" = u."BranchId"
                        WHERE u."Id" = NEW."StaffId" AND u."IsActive" AND b."IsActive"
                          AND u."Role" IN ('SalesAgent', 'BranchManager') AND u."BranchId" = appointment."BranchId")
                        OR NOT EXISTS (SELECT 1 FROM public."AspNetUsers" WHERE "Id" = appointment."OwnerId"
                            AND "IsActive" AND "EmailConfirmed" AND "Role" = 'Customer') THEN
                        RAISE EXCEPTION 'Check-in requires current branch staff and an active verified customer.'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_AppointmentCheckIns_Eligibility';
                    END IF;
                    IF NEW."CorrectionRecordedAt" IS NOT NULL AND (
                        NOT EXISTS (SELECT 1 FROM public."AspNetUsers" WHERE "Id" = NEW."StaffId" AND "Role" = 'BranchManager')
                        OR NEW."CorrectionRecordedAt" < ((appointment."StartsAt" AT TIME ZONE 'Asia/Kuala_Lumpur')::date + 1)::timestamp
                            AT TIME ZONE 'Asia/Kuala_Lumpur'
                        OR NEW."CorrectionRecordedAt" > clock_timestamp()) THEN
                        RAISE EXCEPTION 'No-show correction requires the current same-branch manager after the booked day ends.'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_AppointmentCheckIns_Time';
                    END IF;
                    IF (NEW."CheckedInAt" AT TIME ZONE 'Asia/Kuala_Lumpur')::date <>
                            (appointment."StartsAt" AT TIME ZONE 'Asia/Kuala_Lumpur')::date
                        OR (NEW."CorrectionRecordedAt" IS NULL AND (clock_timestamp() AT TIME ZONE 'Asia/Kuala_Lumpur')::date <>
                            (appointment."StartsAt" AT TIME ZONE 'Asia/Kuala_Lumpur')::date)
                        OR NEW."CheckedInAt" < appointment."CreatedAt" OR NEW."CheckedInAt" > clock_timestamp()
                        OR EXISTS (SELECT 1 FROM public."AppointmentCancellations" WHERE "AppointmentId" = NEW."AppointmentId") THEN
                        RAISE EXCEPTION 'Only an uncancelled appointment on its booked Malaysian date can be checked in.'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_AppointmentCheckIns_Time';
                    END IF;
                    FOR photo_id IN SELECT "PhotoId" FROM public."AppointmentPhotos"
                        WHERE "AppointmentId" = NEW."AppointmentId" ORDER BY "PhotoId" LOOP
                        UPDATE public."IntakePhotos" SET "AvailableAt" = "AvailableAt" WHERE "Id" = photo_id;
                        IF NOT EXISTS (SELECT 1 FROM public."IntakePhotos" WHERE "Id" = photo_id AND "AvailableAt" IS NOT NULL)
                            OR EXISTS (SELECT 1 FROM public."PhotoRemovals" WHERE "PhotoId" = photo_id) THEN
                            RAISE EXCEPTION 'Check-in requires available attached evidence without removal intent.'
                                USING ERRCODE = '23514', CONSTRAINT = 'CK_AppointmentCheckIns_Photos';
                        END IF;
                    END LOOP;
                    RETURN NEW;
                END;
                $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION public.validate_appointment_checkin() RETURNS trigger
                LANGUAGE plpgsql SET search_path = pg_catalog AS $$
                DECLARE
                    appointment public."IntakeAppointments"%ROWTYPE;
                    photo_id uuid;
                BEGIN
                    SELECT * INTO appointment FROM public."IntakeAppointments" WHERE "Id" = NEW."AppointmentId";
                    IF NOT FOUND THEN
                        RETURN NEW;
                    END IF;
                    PERFORM 1 FROM public."AspNetUsers" WHERE "Id" IN (NEW."StaffId", appointment."OwnerId")
                        ORDER BY "Id" FOR SHARE;
                    -- Advance the shared branch version to fence stale cancellation/check-in snapshots.
                    UPDATE public."Branches" SET "IsActive" = "IsActive" WHERE "Id" = appointment."BranchId";
                    IF NOT EXISTS (SELECT 1 FROM public."AspNetUsers" u JOIN public."Branches" b ON b."Id" = u."BranchId"
                        WHERE u."Id" = NEW."StaffId" AND u."IsActive" AND b."IsActive"
                          AND u."Role" IN ('SalesAgent', 'BranchManager') AND u."BranchId" = appointment."BranchId")
                        OR NOT EXISTS (SELECT 1 FROM public."AspNetUsers" WHERE "Id" = appointment."OwnerId"
                            AND "IsActive" AND "EmailConfirmed" AND "Role" = 'Customer') THEN
                        RAISE EXCEPTION 'Check-in requires current branch staff and an active verified customer.'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_AppointmentCheckIns_Eligibility';
                    END IF;
                    IF (NEW."CheckedInAt" AT TIME ZONE 'Asia/Kuala_Lumpur')::date <>
                            (appointment."StartsAt" AT TIME ZONE 'Asia/Kuala_Lumpur')::date
                        OR (clock_timestamp() AT TIME ZONE 'Asia/Kuala_Lumpur')::date <>
                            (appointment."StartsAt" AT TIME ZONE 'Asia/Kuala_Lumpur')::date
                        OR NEW."CheckedInAt" < appointment."CreatedAt" OR NEW."CheckedInAt" > clock_timestamp()
                        OR EXISTS (SELECT 1 FROM public."AppointmentCancellations" WHERE "AppointmentId" = NEW."AppointmentId") THEN
                        RAISE EXCEPTION 'Only an uncancelled appointment on its booked Malaysian date can be checked in.'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_AppointmentCheckIns_Time';
                    END IF;
                    FOR photo_id IN SELECT "PhotoId" FROM public."AppointmentPhotos"
                        WHERE "AppointmentId" = NEW."AppointmentId" ORDER BY "PhotoId" LOOP
                        UPDATE public."IntakePhotos" SET "AvailableAt" = "AvailableAt" WHERE "Id" = photo_id;
                        IF NOT EXISTS (SELECT 1 FROM public."IntakePhotos" WHERE "Id" = photo_id AND "AvailableAt" IS NOT NULL)
                            OR EXISTS (SELECT 1 FROM public."PhotoRemovals" WHERE "PhotoId" = photo_id) THEN
                            RAISE EXCEPTION 'Check-in requires available attached evidence without removal intent.'
                                USING ERRCODE = '23514', CONSTRAINT = 'CK_AppointmentCheckIns_Photos';
                        END IF;
                    END LOOP;
                    RETURN NEW;
                END;
                $$;
                """);

            migrationBuilder.DropCheckConstraint(
                name: "CK_AppointmentCheckIns_Correction",
                table: "AppointmentCheckIns");

            migrationBuilder.DropColumn(
                name: "CorrectionReason",
                table: "AppointmentCheckIns");

            migrationBuilder.DropColumn(
                name: "CorrectionRecordedAt",
                table: "AppointmentCheckIns");
        }
    }
}

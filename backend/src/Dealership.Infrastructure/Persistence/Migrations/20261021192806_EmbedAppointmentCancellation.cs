using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dealership.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EmbedAppointmentCancellation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                LOCK TABLE public."IntakeAppointments", public."AppointmentCancellations", public."AppointmentMailOutbox",
                    public."AppointmentCheckIns", public."AppointmentPhotos", public."PhotoRemovals" IN ACCESS EXCLUSIVE MODE;
                DROP TRIGGER protect_appointment ON public."IntakeAppointments";
                """);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CancellationPhotosExpireAt",
                table: "IntakeAppointments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CancelledAt",
                table: "IntakeAppointments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_IntakeAppointments_Cancellation",
                table: "IntakeAppointments",
                sql: "(\"CancelledAt\" IS NULL AND \"CancellationPhotosExpireAt\" IS NULL) OR (\"CancelledAt\" IS NOT NULL AND \"CancellationPhotosExpireAt\" IS NOT NULL AND \"CancellationPhotosExpireAt\" > \"CancelledAt\")");
            migrationBuilder.Sql("""
                UPDATE public."IntakeAppointments" a SET "CancelledAt" = c."CancelledAt",
                    "CancellationPhotosExpireAt" = c."PhotosExpireAt"
                FROM public."AppointmentCancellations" c WHERE (c."AppointmentId", c."OwnerId") = (a."Id", a."OwnerId");
                """);

            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION public.validate_appointment_cancellation() RETURNS trigger
                LANGUAGE plpgsql SET search_path = pg_catalog AS $$
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        RAISE EXCEPTION 'Appointment evidence is immutable.'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_IntakeAppointments_Immutable';
                    END IF;
                    IF TG_OP = 'INSERT' THEN
                        IF NEW."CancelledAt" IS NOT NULL OR NEW."CancellationPhotosExpireAt" IS NOT NULL THEN
                            RAISE EXCEPTION 'Cancellation must follow a retained booking.'
                                USING ERRCODE = '23514', CONSTRAINT = 'CK_IntakeAppointments_CancellationTime';
                        END IF;
                        RETURN NEW;
                    END IF;
                    IF (to_jsonb(NEW) - 'CancelledAt' - 'CancellationPhotosExpireAt') IS DISTINCT FROM
                        (to_jsonb(OLD) - 'CancelledAt' - 'CancellationPhotosExpireAt')
                        OR (OLD."CancelledAt" IS NOT NULL AND
                            (NEW."CancelledAt", NEW."CancellationPhotosExpireAt") IS DISTINCT FROM
                            (OLD."CancelledAt", OLD."CancellationPhotosExpireAt")) THEN
                        RAISE EXCEPTION 'Booking evidence and first cancellation are immutable.'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_IntakeAppointments_Immutable';
                    END IF;
                    IF OLD."CancelledAt" IS NOT NULL OR NEW."CancelledAt" IS NULL THEN
                        RETURN NEW;
                    END IF;
                    -- Advance the shared branch version before reading competing check-in evidence.
                    UPDATE public."Branches" SET "IsActive" = "IsActive" WHERE "Id" = NEW."BranchId";
                    IF EXISTS (SELECT 1 FROM public."AppointmentCheckIns" WHERE "AppointmentId" = NEW."Id") THEN
                        RAISE EXCEPTION 'A checked-in appointment cannot be cancelled.'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_IntakeAppointments_CheckedIn';
                    END IF;
                    IF NEW."CancelledAt" < NEW."CreatedAt" OR NEW."CancelledAt" >= NEW."StartsAt"
                        OR clock_timestamp() >= NEW."StartsAt" THEN
                        RAISE EXCEPTION 'Cancellation must precede appointment start.'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_IntakeAppointments_CancellationTime';
                    END IF;
                    RETURN NEW;
                END;
                $$;
                """);

            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION public.protect_appointment_slot() RETURNS trigger
                LANGUAGE plpgsql SET search_path = pg_catalog AS $$
                BEGIN
                    -- Advance the row version so stale repeatable-read writers fail instead of missing allocations.
                    UPDATE public."Branches" SET "IsActive" = "IsActive" WHERE "Id" = NEW."BranchId";
                    IF EXISTS (SELECT 1 FROM public."IntakeAppointments" a
                        WHERE a."BranchId" = NEW."BranchId" AND a."StartsAt" = NEW."StartsAt"
                          AND a."CancelledAt" IS NULL) THEN
                        RAISE EXCEPTION 'The branch slot is occupied.'
                            USING ERRCODE = '23505', CONSTRAINT = 'IX_IntakeAppointments_BranchId_StartsAt';
                    END IF;
                    IF EXISTS (SELECT 1 FROM public."IntakeAppointments" a
                        WHERE a."BranchId" = NEW."BranchId" AND a."StartsAt" < NEW."EndsAt" AND NEW."StartsAt" < a."EndsAt"
                          AND a."CancelledAt" IS NULL) THEN
                        RAISE EXCEPTION 'The branch already has an overlapping appointment.'
                            USING ERRCODE = '23P01', CONSTRAINT = 'EX_IntakeAppointments_Slot';
                    END IF;
                    RETURN NEW;
                END;
                $$;

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
                        OR EXISTS (SELECT 1 FROM public."IntakeAppointments" WHERE "Id" = NEW."AppointmentId" AND "CancelledAt" IS NOT NULL) THEN
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

                CREATE OR REPLACE FUNCTION public.validate_photo_removal() RETURNS trigger
                LANGUAGE plpgsql SET search_path = pg_catalog AS $$
                DECLARE photo public."IntakePhotos"%ROWTYPE;
                BEGIN
                    -- Fence stale attachment snapshots while preserving immutable photo metadata.
                    UPDATE public."IntakePhotos" SET "AvailableAt" = "AvailableAt"
                        WHERE "Id" = NEW."PhotoId" RETURNING * INTO photo;
                    IF NOT FOUND THEN
                        RETURN NEW;
                    END IF;
                    IF NEW."EligibleAt" > clock_timestamp()
                        OR (NEW."Reason" = 'UnsubmittedExpired' AND
                        (NEW."EligibleAt" <> photo."ExpiresAt" OR EXISTS
                            (SELECT 1 FROM public."AppointmentPhotos" WHERE "PhotoId" = NEW."PhotoId")))
                        OR (NEW."Reason" = 'CancelledRetentionExpired' AND NOT EXISTS
                            (SELECT 1 FROM public."AppointmentPhotos" a JOIN public."IntakeAppointments" c
                                ON c."Id" = a."AppointmentId"
                             WHERE a."PhotoId" = NEW."PhotoId" AND NEW."EligibleAt" = c."CancellationPhotosExpireAt")) THEN
                        RAISE EXCEPTION 'Only expired unsubmitted or cancelled appointment photos may be removed.'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_PhotoRemovals_Eligibility';
                    END IF;
                    RETURN NEW;
                END;
                $$;

                CREATE OR REPLACE FUNCTION public.require_cancellation_mail() RETURNS trigger
                LANGUAGE plpgsql SET search_path = pg_catalog AS $$
                BEGIN
                    IF NEW."CancelledAt" IS NOT NULL AND NOT EXISTS (SELECT 1 FROM public."AppointmentMailOutbox" WHERE "AppointmentId" = NEW."Id" AND "Kind" = 'Cancellation') THEN
                        RAISE EXCEPTION 'Cancellation requires durable mail intent.'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_IntakeAppointments_CancellationMail';
                    END IF;
                    RETURN NULL;
                END;
                $$;

                CREATE FUNCTION public.require_cancelled_mail_parent() RETURNS trigger
                LANGUAGE plpgsql SET search_path = pg_catalog AS $$
                BEGIN
                    IF NEW."Kind" = 'Cancellation' AND NOT EXISTS (
                        SELECT 1 FROM public."IntakeAppointments" WHERE "Id" = NEW."AppointmentId" AND "CancelledAt" IS NOT NULL) THEN
                        RAISE EXCEPTION 'Cancellation mail requires a cancelled appointment.'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_AppointmentMailOutbox_Cancelled';
                    END IF;
                    RETURN NULL;
                END;
                $$;
                CREATE CONSTRAINT TRIGGER require_cancelled_mail_parent AFTER INSERT ON public."AppointmentMailOutbox"
                    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION public.require_cancelled_mail_parent();

                CREATE TRIGGER protect_appointment BEFORE INSERT OR UPDATE OR DELETE ON public."IntakeAppointments"
                    FOR EACH ROW EXECUTE FUNCTION public.validate_appointment_cancellation();
                CREATE CONSTRAINT TRIGGER require_cancellation_mail AFTER INSERT OR UPDATE ON public."IntakeAppointments"
                    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION public.require_cancellation_mail();
                ALTER TABLE public."AppointmentMailOutbox" DROP CONSTRAINT "FK_AppointmentMailOutbox_AppointmentCancellations_Cancellation~";
                ALTER TABLE public."AppointmentMailOutbox" DROP COLUMN "CancellationAppointmentId";
                DROP TABLE public."AppointmentCancellations";
                DROP FUNCTION public.prevent_checked_in_cancellation();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                LOCK TABLE public."IntakeAppointments", public."AppointmentMailOutbox", public."AppointmentCheckIns",
                    public."AppointmentPhotos", public."PhotoRemovals" IN ACCESS EXCLUSIVE MODE;
                DROP TRIGGER protect_appointment ON public."IntakeAppointments";
                DROP TRIGGER require_cancellation_mail ON public."IntakeAppointments";
                DROP TRIGGER require_cancelled_mail_parent ON public."AppointmentMailOutbox";
                DROP FUNCTION public.require_cancelled_mail_parent();
                """);

            migrationBuilder.AddColumn<Guid>(
                name: "CancellationAppointmentId",
                table: "AppointmentMailOutbox",
                type: "uuid",
                nullable: true,
                computedColumnSql: "CASE WHEN \"Kind\" = 'Cancellation' THEN \"AppointmentId\" END",
                stored: true);

            migrationBuilder.CreateTable(
                name: "AppointmentCancellations",
                columns: table => new
                {
                    AppointmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    CancelledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    PhotosExpireAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppointmentCancellations", x => x.AppointmentId);
                    table.CheckConstraint("CK_AppointmentCancellations_Retention", "\"PhotosExpireAt\" > \"CancelledAt\"");
                    table.ForeignKey(
                        name: "FK_AppointmentCancellations_IntakeAppointments_AppointmentId_O~",
                        columns: x => new { x.AppointmentId, x.OwnerId },
                        principalTable: "IntakeAppointments",
                        principalColumns: ["Id", "OwnerId"],
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentMailOutbox_CancellationAppointmentId",
                table: "AppointmentMailOutbox",
                column: "CancellationAppointmentId");

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
                INSERT INTO public."AppointmentCancellations" ("AppointmentId", "OwnerId", "CancelledAt", "PhotosExpireAt")
                SELECT "Id", "OwnerId", "CancelledAt", "CancellationPhotosExpireAt" FROM public."IntakeAppointments" WHERE "CancelledAt" IS NOT NULL;
                DO $$
                DECLARE permission record;
                BEGIN
                    FOR permission IN SELECT grantee, privilege_type FROM information_schema.table_privileges
                        WHERE table_schema = 'public' AND table_name = 'IntakeAppointments' AND grantee <> current_user LOOP
                        EXECUTE format('GRANT %s ON TABLE public."AppointmentCancellations" TO %s', permission.privilege_type,
                            CASE WHEN permission.grantee = 'PUBLIC' THEN 'PUBLIC' ELSE quote_ident(permission.grantee) END);
                    END LOOP;
                END;
                $$;
                """);
            migrationBuilder.AddForeignKey(
                name: "FK_AppointmentMailOutbox_AppointmentCancellations_Cancellation~",
                table: "AppointmentMailOutbox",
                column: "CancellationAppointmentId",
                principalTable: "AppointmentCancellations",
                principalColumn: "AppointmentId",
                onDelete: ReferentialAction.Restrict);
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

                CREATE OR REPLACE FUNCTION public.validate_photo_removal() RETURNS trigger
                LANGUAGE plpgsql SET search_path = pg_catalog AS $$
                DECLARE photo public."IntakePhotos"%ROWTYPE;
                BEGIN
                    -- Fence stale attachment snapshots while preserving immutable photo metadata.
                    UPDATE public."IntakePhotos" SET "AvailableAt" = "AvailableAt"
                        WHERE "Id" = NEW."PhotoId" RETURNING * INTO photo;
                    IF NOT FOUND THEN
                        RETURN NEW;
                    END IF;
                    IF NEW."EligibleAt" > clock_timestamp()
                        OR (NEW."Reason" = 'UnsubmittedExpired' AND
                        (NEW."EligibleAt" <> photo."ExpiresAt" OR EXISTS
                            (SELECT 1 FROM public."AppointmentPhotos" WHERE "PhotoId" = NEW."PhotoId")))
                        OR (NEW."Reason" = 'CancelledRetentionExpired' AND NOT EXISTS
                            (SELECT 1 FROM public."AppointmentPhotos" a JOIN public."AppointmentCancellations" c
                                ON c."AppointmentId" = a."AppointmentId"
                             WHERE a."PhotoId" = NEW."PhotoId" AND NEW."EligibleAt" = c."PhotosExpireAt")) THEN
                        RAISE EXCEPTION 'Only expired unsubmitted or cancelled appointment photos may be removed.'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_PhotoRemovals_Eligibility';
                    END IF;
                    RETURN NEW;
                END;
                $$;

                CREATE OR REPLACE FUNCTION public.require_cancellation_mail() RETURNS trigger
                LANGUAGE plpgsql SET search_path = pg_catalog AS $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM public."AppointmentMailOutbox" WHERE "AppointmentId" = NEW."AppointmentId" AND "Kind" = 'Cancellation') THEN
                        RAISE EXCEPTION 'Cancellation requires durable mail intent.'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_AppointmentCancellations_Mail';
                    END IF;
                    RETURN NULL;
                END;
                $$;

                CREATE OR REPLACE FUNCTION public.validate_appointment_cancellation() RETURNS trigger
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

                CREATE OR REPLACE FUNCTION public.prevent_checked_in_cancellation() RETURNS trigger
                LANGUAGE plpgsql SET search_path = pg_catalog AS $$
                BEGIN
                    UPDATE public."Branches" SET "IsActive" = "IsActive"
                        WHERE "Id" = (SELECT "BranchId" FROM public."IntakeAppointments" WHERE "Id" = NEW."AppointmentId");
                    IF EXISTS (SELECT 1 FROM public."AppointmentCheckIns" WHERE "AppointmentId" = NEW."AppointmentId") THEN
                        RAISE EXCEPTION 'A checked-in appointment cannot be cancelled.'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_AppointmentCancellations_CheckedIn';
                    END IF;
                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER protect_appointment BEFORE UPDATE OR DELETE ON public."IntakeAppointments"
                    FOR EACH ROW EXECUTE FUNCTION public.protect_appointment_evidence();
                CREATE TRIGGER validate_appointment_cancellation BEFORE INSERT ON public."AppointmentCancellations"
                    FOR EACH ROW EXECUTE FUNCTION public.validate_appointment_cancellation();
                CREATE TRIGGER prevent_checked_in_cancellation BEFORE INSERT ON public."AppointmentCancellations"
                    FOR EACH ROW EXECUTE FUNCTION public.prevent_checked_in_cancellation();
                CREATE TRIGGER protect_appointment_cancellation BEFORE UPDATE OR DELETE ON public."AppointmentCancellations"
                    FOR EACH ROW EXECUTE FUNCTION public.protect_appointment_evidence();
                CREATE CONSTRAINT TRIGGER require_cancellation_mail AFTER INSERT ON public."AppointmentCancellations"
                    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION public.require_cancellation_mail();
                """);

            migrationBuilder.DropCheckConstraint(
                name: "CK_IntakeAppointments_Cancellation",
                table: "IntakeAppointments");

            migrationBuilder.DropColumn(
                name: "CancellationPhotosExpireAt",
                table: "IntakeAppointments");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "IntakeAppointments");

        }
    }
}

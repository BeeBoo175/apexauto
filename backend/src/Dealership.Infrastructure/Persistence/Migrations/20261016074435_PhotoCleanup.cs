using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dealership.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PhotoCleanup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PhotosExpireAt",
                table: "AppointmentCancellations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql("""
                ALTER TABLE public."AppointmentCancellations" DISABLE TRIGGER protect_appointment_cancellation;
                UPDATE public."AppointmentCancellations" SET "PhotosExpireAt" = "CancelledAt" + interval '30 days';
                ALTER TABLE public."AppointmentCancellations" ENABLE TRIGGER protect_appointment_cancellation;
                """);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "PhotosExpireAt",
                table: "AppointmentCancellations",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_AppointmentCancellations_Retention",
                table: "AppointmentCancellations",
                sql: "\"PhotosExpireAt\" > \"CancelledAt\"");

            migrationBuilder.CreateTable(
                name: "PhotoRemovals",
                columns: table => new
                {
                    PhotoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EligibleAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ClaimId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClaimExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhotoRemovals", x => x.PhotoId);
                    table.CheckConstraint("CK_PhotoRemovals_Shape", "\"Reason\" IN ('UnsubmittedExpired', 'CancelledRetentionExpired') AND \"RequestedAt\" >= \"EligibleAt\" AND \"Attempts\" >= 0 AND (\"DeletedAt\" IS NULL OR \"DeletedAt\" >= \"RequestedAt\") AND ((\"ClaimId\" IS NULL) = (\"ClaimExpiresAt\" IS NULL))");
                    table.ForeignKey(
                        name: "FK_PhotoRemovals_IntakePhotos_PhotoId",
                        column: x => x.PhotoId,
                        principalTable: "IntakePhotos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PhotoRemovals_NextAttemptAt_PhotoId",
                table: "PhotoRemovals",
                columns: ["NextAttemptAt", "PhotoId"]);

            migrationBuilder.Sql("""
                CREATE FUNCTION public.validate_photo_removal() RETURNS trigger
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
                CREATE TRIGGER validate_photo_removal BEFORE INSERT ON public."PhotoRemovals"
                    FOR EACH ROW EXECUTE FUNCTION public.validate_photo_removal();

                CREATE FUNCTION public.protect_photo_removal() RETURNS trigger
                LANGUAGE plpgsql SET search_path = pg_catalog AS $$
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        RAISE EXCEPTION 'Photo removal history cannot be deleted.'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_PhotoRemovals_Immutable';
                    END IF;
                    IF (NEW."PhotoId", NEW."Reason", NEW."EligibleAt", NEW."RequestedAt") IS DISTINCT FROM
                        (OLD."PhotoId", OLD."Reason", OLD."EligibleAt", OLD."RequestedAt")
                        OR NEW."Attempts" < OLD."Attempts"
                        OR (OLD."DeletedAt" IS NOT NULL AND NEW."DeletedAt" IS DISTINCT FROM OLD."DeletedAt") THEN
                        RAISE EXCEPTION 'Photo removal identity and first deletion receipt are immutable.'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_PhotoRemovals_Immutable';
                    END IF;
                    RETURN NEW;
                END;
                $$;
                CREATE TRIGGER protect_photo_removal BEFORE UPDATE OR DELETE ON public."PhotoRemovals"
                    FOR EACH ROW EXECUTE FUNCTION public.protect_photo_removal();

                CREATE OR REPLACE FUNCTION public.require_available_appointment_photo() RETURNS trigger
                LANGUAGE plpgsql SET search_path = pg_catalog AS $$
                DECLARE photo public."IntakePhotos"%ROWTYPE;
                BEGIN
                    UPDATE public."IntakePhotos" SET "AvailableAt" = "AvailableAt"
                        WHERE "Id" = NEW."PhotoId" RETURNING * INTO photo;
                    IF FOUND AND (photo."AvailableAt" IS NULL OR photo."AvailableAt" > clock_timestamp()
                        OR photo."ExpiresAt" <= clock_timestamp() OR EXISTS
                            (SELECT 1 FROM public."PhotoRemovals" WHERE "PhotoId" = NEW."PhotoId")) THEN
                        RAISE EXCEPTION 'Only currently available unsubmitted photos can be attached.'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_AppointmentPhotos_Available';
                    END IF;
                    RETURN NEW;
                END;
                $$;

                CREATE FUNCTION public.prevent_removed_photo_availability() RETURNS trigger
                LANGUAGE plpgsql SET search_path = pg_catalog AS $$
                BEGIN
                    IF OLD."AvailableAt" IS NULL AND NEW."AvailableAt" IS NOT NULL
                        AND EXISTS (SELECT 1 FROM public."PhotoRemovals" WHERE "PhotoId" = NEW."Id") THEN
                        RAISE EXCEPTION 'A photo with removal intent cannot become available.'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_IntakePhotos_Removal';
                    END IF;
                    RETURN NEW;
                END;
                $$;
                CREATE TRIGGER prevent_removed_photo_availability BEFORE UPDATE ON public."IntakePhotos"
                    FOR EACH ROW EXECUTE FUNCTION public.prevent_removed_photo_availability();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP FUNCTION public.validate_photo_removal() CASCADE;
                DROP FUNCTION public.protect_photo_removal() CASCADE;
                DROP FUNCTION public.prevent_removed_photo_availability() CASCADE;
                CREATE OR REPLACE FUNCTION public.require_available_appointment_photo() RETURNS trigger
                LANGUAGE plpgsql SET search_path = pg_catalog AS $$
                DECLARE photo public."IntakePhotos"%ROWTYPE;
                BEGIN
                    SELECT * INTO photo FROM public."IntakePhotos" WHERE "Id" = NEW."PhotoId" FOR UPDATE;
                    IF FOUND AND (photo."AvailableAt" IS NULL OR photo."AvailableAt" > clock_timestamp()
                        OR photo."ExpiresAt" <= clock_timestamp()) THEN
                        RAISE EXCEPTION 'Only currently available unsubmitted photos can be attached.'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_AppointmentPhotos_Available';
                    END IF;
                    RETURN NEW;
                END;
                $$;
                """);
            migrationBuilder.DropTable(
                name: "PhotoRemovals");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AppointmentCancellations_Retention",
                table: "AppointmentCancellations");

            migrationBuilder.DropColumn(
                name: "PhotosExpireAt",
                table: "AppointmentCancellations");
        }
    }
}

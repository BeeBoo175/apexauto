using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dealership.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OwnerErasedUploads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PhotoRemovals_Shape",
                table: "PhotoRemovals");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AccountPictures_Content",
                table: "AccountPictures");

            migrationBuilder.AlterColumn<string>(
                name: "Sha256",
                table: "AccountPictures",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AddCheckConstraint(
                name: "CK_PhotoRemovals_Shape",
                table: "PhotoRemovals",
                sql: "\"Reason\" IN ('UnsubmittedExpired', 'CancelledRetentionExpired', 'OwnerErased') AND \"RequestedAt\" >= \"EligibleAt\" AND \"Attempts\" >= 0 AND (\"DeletedAt\" IS NULL OR \"DeletedAt\" >= \"RequestedAt\") AND ((\"ClaimId\" IS NULL) = (\"ClaimExpiresAt\" IS NULL))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AccountPictures_Content",
                table: "AccountPictures",
                sql: "\"Bytes\" > 0 AND (\"Sha256\" IS NULL OR \"Sha256\" ~ '^[0-9a-f]{64}$') AND \"ContentType\" IN ('image/jpeg', 'image/png', 'image/webp')");

            // A new receipt reason: the owner is closed and the photo is unattached or belongs to an appointment that
            // never happened. Attached evidence of appointments that took place stays ineligible.
            migrationBuilder.Sql("""
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
                             WHERE a."PhotoId" = NEW."PhotoId" AND NEW."EligibleAt" = c."CancellationPhotosExpireAt"))
                        OR (NEW."Reason" = 'OwnerErased' AND (NOT EXISTS
                            (SELECT 1 FROM public."AspNetUsers" u
                             WHERE u."Id" = photo."OwnerId" AND u."ClosedAt" IS NOT NULL AND u."ClosedAt" = NEW."EligibleAt")
                            OR EXISTS
                            (SELECT 1 FROM public."AppointmentPhotos" a JOIN public."IntakeAppointments" c
                                ON c."Id" = a."AppointmentId"
                             WHERE a."PhotoId" = NEW."PhotoId" AND (c."CancelledAt" IS NOT NULL OR c."EndsAt" >= NEW."EligibleAt"
                                OR EXISTS (SELECT 1 FROM public."AppointmentCheckIns" k WHERE k."AppointmentId" = c."Id"))))) THEN
                        RAISE EXCEPTION 'Only expired unsubmitted, cancelled or erased-owner appointment photos may be removed.'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_PhotoRemovals_Eligibility';
                    END IF;
                    RETURN NEW;
                END;
                $$;

                CREATE FUNCTION public.guard_account_picture_hash() RETURNS trigger
                LANGUAGE plpgsql SET search_path = pg_catalog AS $$
                BEGIN
                    IF NEW."Sha256" IS NULL AND OLD."Sha256" IS NOT NULL AND NOT (
                        EXISTS (SELECT 1 FROM public."AspNetUsers" WHERE "Id" = OLD."UserId" AND "ClosedAt" IS NOT NULL)
                        AND EXISTS (SELECT 1 FROM public."AccountPictureRemovals"
                            WHERE "PictureId" = OLD."Id" AND "DeletedAt" IS NOT NULL)) THEN
                        RAISE EXCEPTION 'A picture hash is cleared only after the object of a closed owner is deleted.'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_AccountPictures_HashRetained';
                    END IF;
                    RETURN NEW;
                END;
                $$;
                CREATE TRIGGER guard_account_picture_hash BEFORE UPDATE OF "Sha256" ON public."AccountPictures"
                    FOR EACH ROW EXECUTE FUNCTION public.guard_account_picture_hash();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM public."AccountPictures" WHERE "Sha256" IS NULL)
                        OR EXISTS (SELECT 1 FROM public."PhotoRemovals" WHERE "Reason" = 'OwnerErased') THEN
                        RAISE EXCEPTION 'Erased picture hashes or owner-erased photo removals exist; the migration cannot be reverted.';
                    END IF;
                END
                $$;
                DROP TRIGGER guard_account_picture_hash ON public."AccountPictures";
                DROP FUNCTION public.guard_account_picture_hash();
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
                """);

            migrationBuilder.DropCheckConstraint(
                name: "CK_PhotoRemovals_Shape",
                table: "PhotoRemovals");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AccountPictures_Content",
                table: "AccountPictures");

            migrationBuilder.AlterColumn<string>(
                name: "Sha256",
                table: "AccountPictures",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_PhotoRemovals_Shape",
                table: "PhotoRemovals",
                sql: "\"Reason\" IN ('UnsubmittedExpired', 'CancelledRetentionExpired') AND \"RequestedAt\" >= \"EligibleAt\" AND \"Attempts\" >= 0 AND (\"DeletedAt\" IS NULL OR \"DeletedAt\" >= \"RequestedAt\") AND ((\"ClaimId\" IS NULL) = (\"ClaimExpiresAt\" IS NULL))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AccountPictures_Content",
                table: "AccountPictures",
                sql: "\"Bytes\" > 0 AND \"Sha256\" ~ '^[0-9a-f]{64}$' AND \"ContentType\" IN ('image/jpeg', 'image/png', 'image/webp')");
        }
    }
}

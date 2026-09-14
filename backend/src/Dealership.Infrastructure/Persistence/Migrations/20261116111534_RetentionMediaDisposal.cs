using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dealership.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RetentionMediaDisposal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PhotoRemovals_Shape",
                table: "PhotoRemovals");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InspectionPhotoRemovals_Shape",
                table: "InspectionPhotoRemovals");

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "InspectionPhotoRemovals",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "UnattachedExpired");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PhotoRemovals_Shape",
                table: "PhotoRemovals",
                sql: "\"Reason\" IN ('UnsubmittedExpired', 'CancelledRetentionExpired', 'OwnerErased', 'RetentionExpired') AND \"RequestedAt\" >= \"EligibleAt\" AND \"Attempts\" >= 0 AND (\"DeletedAt\" IS NULL OR \"DeletedAt\" >= \"RequestedAt\") AND ((\"ClaimId\" IS NULL) = (\"ClaimExpiresAt\" IS NULL))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InspectionPhotoRemovals_Shape",
                table: "InspectionPhotoRemovals",
                sql: "\"Reason\" IN ('UnattachedExpired', 'RetentionExpired') AND isfinite(\"EligibleAt\") AND isfinite(\"RequestedAt\") AND \"RequestedAt\" >= \"EligibleAt\" AND \"Attempts\" >= 0 AND isfinite(\"NextAttemptAt\") AND (\"DeletedAt\" IS NULL OR (isfinite(\"DeletedAt\") AND \"DeletedAt\" >= \"RequestedAt\")) AND ((\"ClaimId\" IS NULL) = (\"ClaimExpiresAt\" IS NULL)) AND (\"ClaimId\" IS NULL OR (\"ClaimId\" <> '00000000-0000-0000-0000-000000000000'::uuid AND isfinite(\"ClaimExpiresAt\")))");

            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION guard_inspection_photo_removal() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE photo "InspectionPhotos"%ROWTYPE;
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        RAISE EXCEPTION 'Inspection cleanup history is retained'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionPhotoRemovals_Immutable';
                    END IF;
                    IF TG_OP = 'UPDATE' THEN
                        IF (NEW."PhotoId", NEW."Reason", NEW."EligibleAt", NEW."RequestedAt") IS DISTINCT FROM
                           (OLD."PhotoId", OLD."Reason", OLD."EligibleAt", OLD."RequestedAt") OR NEW."Attempts" < OLD."Attempts" OR
                           (OLD."DeletedAt" IS NOT NULL AND NEW."DeletedAt" IS DISTINCT FROM OLD."DeletedAt") THEN
                            RAISE EXCEPTION 'Inspection cleanup identity and first deletion receipt are immutable'
                                USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionPhotoRemovals_Immutable';
                        END IF;
                        IF OLD."DeletedAt" IS NULL AND NEW."DeletedAt" IS NOT NULL AND
                           (OLD."ClaimId" IS NULL OR OLD."ClaimExpiresAt" <= clock_timestamp() OR
                            NEW."ClaimId" IS DISTINCT FROM OLD."ClaimId" OR NEW."DeletedAt" > clock_timestamp()) THEN
                            RAISE EXCEPTION 'Deletion receipt requires the current unexpired claim'
                                USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionPhotoRemovals_Claim';
                        END IF;
                        RETURN NEW;
                    END IF;
                    -- At retention expiry only a photograph attached to a signed inspection is queued for removal.
                    IF NEW."Reason" = 'RetentionExpired' THEN
                        IF NEW."EligibleAt" > clock_timestamp() OR NEW."RequestedAt" > clock_timestamp() OR
                           NEW."DeletedAt" IS NOT NULL OR NOT EXISTS (SELECT 1 FROM "InspectionDraftPhotos" l
                               JOIN "InspectionSignedRevisions" s ON s."InspectionId" = l."InspectionId"
                               WHERE l."PhotoId" = NEW."PhotoId") THEN
                            RAISE EXCEPTION 'Only photographs of a signed inspection may be removed at retention expiry'
                                USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionPhotoRemovals_Eligibility';
                        END IF;
                        RETURN NEW;
                    END IF;
                    UPDATE "InspectionPhotos" SET "AvailableAt" = "AvailableAt" WHERE "Id" = NEW."PhotoId"
                        RETURNING * INTO photo;
                    IF photo."Id" IS NOT NULL AND (NEW."EligibleAt" <> photo."ExpiresAt" OR
                       NEW."EligibleAt" > clock_timestamp() OR NEW."RequestedAt" > clock_timestamp() OR
                       NEW."DeletedAt" IS NOT NULL OR EXISTS
                       (SELECT 1 FROM "InspectionDraftPhotos" WHERE "PhotoId" = NEW."PhotoId")) THEN
                        RAISE EXCEPTION 'Only expired unattached inspection uploads may be cleaned'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionPhotoRemovals_Eligibility';
                    END IF;
                    RETURN NEW;
                END $$;

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
                    -- At retention expiry only a photograph of a checked-in appointment is queued for removal.
                    IF NEW."Reason" = 'RetentionExpired' THEN
                        IF NEW."EligibleAt" > clock_timestamp() OR NOT EXISTS (SELECT 1 FROM public."AppointmentPhotos" a
                            JOIN public."AppointmentCheckIns" k ON k."AppointmentId" = a."AppointmentId"
                            WHERE a."PhotoId" = NEW."PhotoId") THEN
                            RAISE EXCEPTION 'Only photographs of a checked-in appointment may be removed at retention expiry.'
                                USING ERRCODE = '23514', CONSTRAINT = 'CK_PhotoRemovals_Eligibility';
                        END IF;
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

                -- The technician's sign-in name may leave a signed report once its retention period ends; no other byte of a signed
                -- revision changes and deleting stays refused by the original guard.
                CREATE FUNCTION public.guard_inspection_signed_revision_minimization() RETURNS trigger
                LANGUAGE plpgsql SET search_path = pg_catalog AS $$
                BEGIN
                    IF OLD."ReportJson"->'technicianIdentity' IS NOT NULL AND
                       (to_jsonb(NEW) - 'ReportJson') = (to_jsonb(OLD) - 'ReportJson') AND
                       NEW."ReportJson" = OLD."ReportJson" - 'technicianIdentity' THEN
                        RETURN NEW;
                    END IF;
                    RAISE EXCEPTION 'Signed inspection revisions are immutable'
                        USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionSignedRevisions_Immutable';
                END $$;
                DROP TRIGGER inspection_signed_revision_guard ON "InspectionSignedRevisions";
                CREATE TRIGGER inspection_signed_revision_guard BEFORE INSERT OR DELETE ON "InspectionSignedRevisions"
                    FOR EACH ROW EXECUTE FUNCTION guard_inspection_signed_revision();
                CREATE TRIGGER inspection_signed_revision_minimization BEFORE UPDATE ON "InspectionSignedRevisions"
                    FOR EACH ROW EXECUTE FUNCTION public.guard_inspection_signed_revision_minimization();

                -- Retention expiry is the one reason that may remove a checked-in photograph; its own validation demands the check-in.
                CREATE OR REPLACE FUNCTION public.prevent_checked_in_photo_removal() RETURNS trigger
                LANGUAGE plpgsql SET search_path = pg_catalog AS $$
                BEGIN
                    PERFORM 1 FROM public."IntakePhotos" WHERE "Id" = NEW."PhotoId" FOR UPDATE;
                    IF NEW."Reason" <> 'RetentionExpired' AND EXISTS (SELECT 1 FROM public."AppointmentPhotos" p JOIN public."AppointmentCheckIns" c
                        ON c."AppointmentId" = p."AppointmentId" WHERE p."PhotoId" = NEW."PhotoId") THEN
                        RAISE EXCEPTION 'Checked-in photo evidence cannot be removed by cancellation cleanup.'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_PhotoRemovals_CheckedIn';
                    END IF;
                    RETURN NEW;
                END;
                $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "InspectionPhotoRemovals" WHERE "Reason" = 'RetentionExpired') OR
                       EXISTS (SELECT 1 FROM "PhotoRemovals" WHERE "Reason" = 'RetentionExpired') OR
                       EXISTS (SELECT 1 FROM "InspectionSignedRevisions" WHERE "ReportJson"->'technicianIdentity' IS NULL) THEN
                        RAISE EXCEPTION 'Cannot revert RetentionMediaDisposal while retention removals or minimized reports exist.';
                    END IF;
                END $$;
                DROP TRIGGER inspection_signed_revision_minimization ON "InspectionSignedRevisions";
                DROP FUNCTION public.guard_inspection_signed_revision_minimization();
                DROP TRIGGER inspection_signed_revision_guard ON "InspectionSignedRevisions";
                CREATE TRIGGER inspection_signed_revision_guard BEFORE INSERT OR UPDATE OR DELETE ON "InspectionSignedRevisions"
                    FOR EACH ROW EXECUTE FUNCTION guard_inspection_signed_revision();
                CREATE OR REPLACE FUNCTION guard_inspection_photo_removal() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE photo "InspectionPhotos"%ROWTYPE;
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        RAISE EXCEPTION 'Inspection cleanup history is retained'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionPhotoRemovals_Immutable';
                    END IF;
                    IF TG_OP = 'UPDATE' THEN
                        IF (NEW."PhotoId", NEW."EligibleAt", NEW."RequestedAt") IS DISTINCT FROM
                           (OLD."PhotoId", OLD."EligibleAt", OLD."RequestedAt") OR NEW."Attempts" < OLD."Attempts" OR
                           (OLD."DeletedAt" IS NOT NULL AND NEW."DeletedAt" IS DISTINCT FROM OLD."DeletedAt") THEN
                            RAISE EXCEPTION 'Inspection cleanup identity and first deletion receipt are immutable'
                                USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionPhotoRemovals_Immutable';
                        END IF;
                        IF OLD."DeletedAt" IS NULL AND NEW."DeletedAt" IS NOT NULL AND
                           (OLD."ClaimId" IS NULL OR OLD."ClaimExpiresAt" <= clock_timestamp() OR
                            NEW."ClaimId" IS DISTINCT FROM OLD."ClaimId" OR NEW."DeletedAt" > clock_timestamp()) THEN
                            RAISE EXCEPTION 'Deletion receipt requires the current unexpired claim'
                                USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionPhotoRemovals_Claim';
                        END IF;
                        RETURN NEW;
                    END IF;
                    UPDATE "InspectionPhotos" SET "AvailableAt" = "AvailableAt" WHERE "Id" = NEW."PhotoId"
                        RETURNING * INTO photo;
                    IF photo."Id" IS NOT NULL AND (NEW."EligibleAt" <> photo."ExpiresAt" OR
                       NEW."EligibleAt" > clock_timestamp() OR NEW."RequestedAt" > clock_timestamp() OR
                       NEW."DeletedAt" IS NOT NULL OR EXISTS
                       (SELECT 1 FROM "InspectionDraftPhotos" WHERE "PhotoId" = NEW."PhotoId")) THEN
                        RAISE EXCEPTION 'Only expired unattached inspection uploads may be cleaned'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionPhotoRemovals_Eligibility';
                    END IF;
                    RETURN NEW;
                END $$;

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

                CREATE OR REPLACE FUNCTION public.prevent_checked_in_photo_removal() RETURNS trigger
                LANGUAGE plpgsql SET search_path = pg_catalog AS $$
                BEGIN
                    PERFORM 1 FROM public."IntakePhotos" WHERE "Id" = NEW."PhotoId" FOR UPDATE;
                    IF EXISTS (SELECT 1 FROM public."AppointmentPhotos" p JOIN public."AppointmentCheckIns" c
                        ON c."AppointmentId" = p."AppointmentId" WHERE p."PhotoId" = NEW."PhotoId") THEN
                        RAISE EXCEPTION 'Checked-in photo evidence cannot be removed by cancellation cleanup.'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_PhotoRemovals_CheckedIn';
                    END IF;
                    RETURN NEW;
                END;
                $$;
                """);

            migrationBuilder.DropCheckConstraint(
                name: "CK_PhotoRemovals_Shape",
                table: "PhotoRemovals");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InspectionPhotoRemovals_Shape",
                table: "InspectionPhotoRemovals");

            migrationBuilder.DropColumn(
                name: "Reason",
                table: "InspectionPhotoRemovals");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PhotoRemovals_Shape",
                table: "PhotoRemovals",
                sql: "\"Reason\" IN ('UnsubmittedExpired', 'CancelledRetentionExpired', 'OwnerErased') AND \"RequestedAt\" >= \"EligibleAt\" AND \"Attempts\" >= 0 AND (\"DeletedAt\" IS NULL OR \"DeletedAt\" >= \"RequestedAt\") AND ((\"ClaimId\" IS NULL) = (\"ClaimExpiresAt\" IS NULL))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InspectionPhotoRemovals_Shape",
                table: "InspectionPhotoRemovals",
                sql: "isfinite(\"EligibleAt\") AND isfinite(\"RequestedAt\") AND \"RequestedAt\" >= \"EligibleAt\" AND \"Attempts\" >= 0 AND isfinite(\"NextAttemptAt\") AND (\"DeletedAt\" IS NULL OR (isfinite(\"DeletedAt\") AND \"DeletedAt\" >= \"RequestedAt\")) AND ((\"ClaimId\" IS NULL) = (\"ClaimExpiresAt\" IS NULL)) AND (\"ClaimId\" IS NULL OR (\"ClaimId\" <> '00000000-0000-0000-0000-000000000000'::uuid AND isfinite(\"ClaimExpiresAt\")))");
        }
    }
}

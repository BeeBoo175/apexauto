using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dealership.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MediaLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AccountPictures_UserId",
                table: "AccountPictures");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AvailableAt",
                table: "AccountPictures",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ExpiresAt",
                table: "AccountPictures",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.CreateTable(
                name: "AccountPictureRemovals",
                columns: table => new
                {
                    PictureId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_AccountPictureRemovals", x => x.PictureId);
                    table.CheckConstraint("CK_AccountPictureRemovals_Shape", "\"Reason\" IN ('Removed', 'Replaced', 'UnconfirmedExpired') AND isfinite(\"EligibleAt\") AND isfinite(\"RequestedAt\") AND \"RequestedAt\" >= \"EligibleAt\" AND \"Attempts\" >= 0 AND isfinite(\"NextAttemptAt\") AND (\"DeletedAt\" IS NULL OR (isfinite(\"DeletedAt\") AND \"DeletedAt\" >= \"RequestedAt\")) AND ((\"ClaimId\" IS NULL) = (\"ClaimExpiresAt\" IS NULL)) AND (\"ClaimId\" IS NULL OR (\"ClaimId\" <> '00000000-0000-0000-0000-000000000000'::uuid AND isfinite(\"ClaimExpiresAt\")))");
                    table.ForeignKey(
                        name: "FK_AccountPictureRemovals_AccountPictures_PictureId",
                        column: x => x.PictureId,
                        principalTable: "AccountPictures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ListingPhotoRemovals",
                columns: table => new
                {
                    PhotoId = table.Column<Guid>(type: "uuid", nullable: false),
                    EligibleAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ClaimId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClaimExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ListingPhotoRemovals", x => x.PhotoId);
                    table.CheckConstraint("CK_ListingPhotoRemovals_Shape", "isfinite(\"EligibleAt\") AND isfinite(\"RequestedAt\") AND \"RequestedAt\" >= \"EligibleAt\" AND \"Attempts\" >= 0 AND isfinite(\"NextAttemptAt\") AND (\"DeletedAt\" IS NULL OR (isfinite(\"DeletedAt\") AND \"DeletedAt\" >= \"RequestedAt\")) AND ((\"ClaimId\" IS NULL) = (\"ClaimExpiresAt\" IS NULL)) AND (\"ClaimId\" IS NULL OR (\"ClaimId\" <> '00000000-0000-0000-0000-000000000000'::uuid AND isfinite(\"ClaimExpiresAt\")))");
                    table.ForeignKey(
                        name: "FK_ListingPhotoRemovals_ListingPhotos_PhotoId",
                        column: x => x.PhotoId,
                        principalTable: "ListingPhotos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Existing pictures were confirmed when stored; replaced and removed ones get the removal receipt the
            // cleanup worker now consumes, which also retries any object an earlier inline delete missed.
            migrationBuilder.Sql("""
                UPDATE public."AccountPictures"
                    SET "AvailableAt" = "CreatedAt", "ExpiresAt" = "CreatedAt" + interval '24 hours';
                INSERT INTO public."AccountPictureRemovals"
                    ("PictureId", "Reason", "RequestedAt", "EligibleAt", "Attempts", "NextAttemptAt")
                    SELECT "Id", 'Removed', "RemovedAt", "RemovedAt", 0, "RemovedAt"
                    FROM public."AccountPictures" WHERE "RemovedAt" IS NOT NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_AccountPictures_UserId",
                table: "AccountPictures",
                column: "UserId",
                unique: true,
                filter: "\"RemovedAt\" IS NULL AND \"AvailableAt\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AccountPictures_Lifecycle",
                table: "AccountPictures",
                sql: "isfinite(\"CreatedAt\") AND \"ExpiresAt\" > \"CreatedAt\" AND (\"AvailableAt\" IS NULL OR (isfinite(\"AvailableAt\") AND \"AvailableAt\" >= \"CreatedAt\")) AND (\"RemovedAt\" IS NULL OR (\"AvailableAt\" IS NOT NULL AND \"RemovedAt\" >= \"AvailableAt\"))");

            migrationBuilder.CreateIndex(
                name: "IX_AccountPictureRemovals_NextAttemptAt_PictureId",
                table: "AccountPictureRemovals",
                columns: ["NextAttemptAt", "PictureId"]);

            migrationBuilder.CreateIndex(
                name: "IX_ListingPhotoRemovals_NextAttemptAt_PhotoId",
                table: "ListingPhotoRemovals",
                columns: ["NextAttemptAt", "PhotoId"]);

            migrationBuilder.Sql("""
                CREATE FUNCTION public.guard_account_picture_removal() RETURNS trigger
                LANGUAGE plpgsql SET search_path = pg_catalog AS $$
                DECLARE picture public."AccountPictures"%ROWTYPE;
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        RAISE EXCEPTION 'Account picture removal history is retained.'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_AccountPictureRemovals_Immutable';
                    END IF;
                    IF TG_OP = 'UPDATE' THEN
                        IF (NEW."PictureId", NEW."Reason", NEW."EligibleAt", NEW."RequestedAt") IS DISTINCT FROM
                           (OLD."PictureId", OLD."Reason", OLD."EligibleAt", OLD."RequestedAt")
                           OR NEW."Attempts" < OLD."Attempts"
                           OR (OLD."DeletedAt" IS NOT NULL AND NEW."DeletedAt" IS DISTINCT FROM OLD."DeletedAt") THEN
                            RAISE EXCEPTION 'Account picture removal identity and first deletion receipt are immutable.'
                                USING ERRCODE = '23514', CONSTRAINT = 'CK_AccountPictureRemovals_Immutable';
                        END IF;
                        IF OLD."DeletedAt" IS NULL AND NEW."DeletedAt" IS NOT NULL AND
                           (OLD."ClaimId" IS NULL OR OLD."ClaimExpiresAt" <= clock_timestamp() OR
                            NEW."ClaimId" IS DISTINCT FROM OLD."ClaimId" OR NEW."DeletedAt" > clock_timestamp()) THEN
                            RAISE EXCEPTION 'Deletion receipt requires the current unexpired claim.'
                                USING ERRCODE = '23514', CONSTRAINT = 'CK_AccountPictureRemovals_Claim';
                        END IF;
                        RETURN NEW;
                    END IF;
                    -- Lock the picture so a concurrent confirmation observes this removal intent.
                    SELECT * INTO picture FROM public."AccountPictures" WHERE "Id" = NEW."PictureId" FOR UPDATE;
                    IF picture."Id" IS NOT NULL AND (NEW."EligibleAt" > clock_timestamp()
                        OR NEW."RequestedAt" > clock_timestamp() OR NEW."DeletedAt" IS NOT NULL
                        OR (NEW."Reason" IN ('Removed', 'Replaced') AND
                            (picture."RemovedAt" IS NULL OR NEW."EligibleAt" <> picture."RemovedAt"))
                        OR (NEW."Reason" = 'UnconfirmedExpired' AND
                            (picture."AvailableAt" IS NOT NULL OR picture."RemovedAt" IS NOT NULL
                             OR NEW."EligibleAt" <> picture."ExpiresAt"))) THEN
                        RAISE EXCEPTION 'Only removed, replaced or expired unconfirmed pictures may be cleaned.'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_AccountPictureRemovals_Eligibility';
                    END IF;
                    RETURN NEW;
                END;
                $$;
                CREATE TRIGGER account_picture_removal_guard BEFORE INSERT OR UPDATE OR DELETE ON public."AccountPictureRemovals"
                    FOR EACH ROW EXECUTE FUNCTION public.guard_account_picture_removal();

                CREATE FUNCTION public.prevent_removed_account_picture_availability() RETURNS trigger
                LANGUAGE plpgsql SET search_path = pg_catalog AS $$
                BEGIN
                    IF OLD."AvailableAt" IS NULL AND NEW."AvailableAt" IS NOT NULL AND EXISTS
                        (SELECT 1 FROM public."AccountPictureRemovals" WHERE "PictureId" = NEW."Id") THEN
                        RAISE EXCEPTION 'A picture with removal intent cannot become available.'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_AccountPictures_Removal';
                    END IF;
                    RETURN NEW;
                END;
                $$;
                CREATE TRIGGER prevent_removed_account_picture_availability BEFORE UPDATE ON public."AccountPictures"
                    FOR EACH ROW EXECUTE FUNCTION public.prevent_removed_account_picture_availability();

                CREATE FUNCTION public.guard_listing_photo_removal() RETURNS trigger
                LANGUAGE plpgsql SET search_path = pg_catalog AS $$
                DECLARE photo public."ListingPhotos"%ROWTYPE;
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        RAISE EXCEPTION 'Listing upload cleanup history is retained.'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_ListingPhotoRemovals_Immutable';
                    END IF;
                    IF TG_OP = 'UPDATE' THEN
                        IF (NEW."PhotoId", NEW."EligibleAt", NEW."RequestedAt") IS DISTINCT FROM
                           (OLD."PhotoId", OLD."EligibleAt", OLD."RequestedAt") OR NEW."Attempts" < OLD."Attempts" OR
                           (OLD."DeletedAt" IS NOT NULL AND NEW."DeletedAt" IS DISTINCT FROM OLD."DeletedAt") THEN
                            RAISE EXCEPTION 'Listing upload cleanup identity and first deletion receipt are immutable.'
                                USING ERRCODE = '23514', CONSTRAINT = 'CK_ListingPhotoRemovals_Immutable';
                        END IF;
                        IF OLD."DeletedAt" IS NULL AND NEW."DeletedAt" IS NOT NULL AND
                           (OLD."ClaimId" IS NULL OR OLD."ClaimExpiresAt" <= clock_timestamp() OR
                            NEW."ClaimId" IS DISTINCT FROM OLD."ClaimId" OR NEW."DeletedAt" > clock_timestamp()) THEN
                            RAISE EXCEPTION 'Deletion receipt requires the current unexpired claim.'
                                USING ERRCODE = '23514', CONSTRAINT = 'CK_ListingPhotoRemovals_Claim';
                        END IF;
                        RETURN NEW;
                    END IF;
                    -- Lock the photo so a concurrent confirmation observes this removal intent.
                    SELECT * INTO photo FROM public."ListingPhotos" WHERE "Id" = NEW."PhotoId" FOR UPDATE;
                    IF photo."Id" IS NOT NULL AND (photo."AvailableAt" IS NOT NULL OR NEW."EligibleAt" < photo."CreatedAt"
                        OR NEW."EligibleAt" > clock_timestamp() OR NEW."RequestedAt" > clock_timestamp()
                        OR NEW."DeletedAt" IS NOT NULL OR EXISTS
                        (SELECT 1 FROM public."ListingRevisionPhotos" WHERE "PhotoId" = NEW."PhotoId")) THEN
                        RAISE EXCEPTION 'Only abandoned unreferenced listing uploads may be cleaned.'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_ListingPhotoRemovals_Eligibility';
                    END IF;
                    RETURN NEW;
                END;
                $$;
                CREATE TRIGGER listing_photo_removal_guard BEFORE INSERT OR UPDATE OR DELETE ON public."ListingPhotoRemovals"
                    FOR EACH ROW EXECUTE FUNCTION public.guard_listing_photo_removal();

                CREATE FUNCTION public.prevent_removed_listing_photo_availability() RETURNS trigger
                LANGUAGE plpgsql SET search_path = pg_catalog AS $$
                BEGIN
                    IF OLD."AvailableAt" IS NULL AND NEW."AvailableAt" IS NOT NULL AND EXISTS
                        (SELECT 1 FROM public."ListingPhotoRemovals" WHERE "PhotoId" = NEW."Id") THEN
                        RAISE EXCEPTION 'A listing upload with removal intent cannot become available.'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_ListingPhotos_Removal';
                    END IF;
                    RETURN NEW;
                END;
                $$;
                CREATE TRIGGER prevent_removed_listing_photo_availability BEFORE UPDATE ON public."ListingPhotos"
                    FOR EACH ROW EXECUTE FUNCTION public.prevent_removed_listing_photo_availability();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The previous schema allows one unremoved row per account, so a pending upload blocks the revert.
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM public."AccountPictures" WHERE "AvailableAt" IS NULL AND "RemovedAt" IS NULL) THEN
                        RAISE EXCEPTION 'Pending account pictures exist; confirm or let them expire before reverting.';
                    END IF;
                END $$;
                DROP FUNCTION public.guard_account_picture_removal() CASCADE;
                DROP FUNCTION public.prevent_removed_account_picture_availability() CASCADE;
                DROP FUNCTION public.guard_listing_photo_removal() CASCADE;
                DROP FUNCTION public.prevent_removed_listing_photo_availability() CASCADE;
                """);


            migrationBuilder.DropTable(
                name: "AccountPictureRemovals");

            migrationBuilder.DropTable(
                name: "ListingPhotoRemovals");

            migrationBuilder.DropIndex(
                name: "IX_AccountPictures_UserId",
                table: "AccountPictures");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AccountPictures_Lifecycle",
                table: "AccountPictures");

            migrationBuilder.DropColumn(
                name: "AvailableAt",
                table: "AccountPictures");

            migrationBuilder.DropColumn(
                name: "ExpiresAt",
                table: "AccountPictures");

            migrationBuilder.CreateIndex(
                name: "IX_AccountPictures_UserId",
                table: "AccountPictures",
                column: "UserId",
                unique: true,
                filter: "\"RemovedAt\" IS NULL");
        }
    }
}

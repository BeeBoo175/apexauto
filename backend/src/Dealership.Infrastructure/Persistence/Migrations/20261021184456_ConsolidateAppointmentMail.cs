using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dealership.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ConsolidateAppointmentMail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppointmentMailOutbox",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AppointmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    RecipientEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    DeliveryAttempts = table.Column<int>(type: "integer", nullable: false),
                    NextDeliveryAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeliveryClaimId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeliveryClaimExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeliveredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancellationAppointmentId = table.Column<Guid>(type: "uuid", nullable: true, computedColumnSql: "CASE WHEN \"Kind\" = 'Cancellation' THEN \"AppointmentId\" END", stored: true),
                    CheckInAppointmentId = table.Column<Guid>(type: "uuid", nullable: true, computedColumnSql: "CASE WHEN \"Kind\" = 'CheckIn' THEN \"AppointmentId\" END", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppointmentMailOutbox", x => x.Id);
                    table.CheckConstraint("CK_AppointmentMailOutbox_Kind", "\"Kind\" IN ('Confirmation', 'Cancellation', 'CheckIn')");
                    table.CheckConstraint("CK_AppointmentMailOutbox_Shape", "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"RecipientEmail\" ~ '[^[:space:]]' AND \"DeliveryAttempts\" >= 0");
                    table.ForeignKey(
                        name: "FK_AppointmentMailOutbox_AppointmentCancellations_Cancellation~",
                        column: x => x.CancellationAppointmentId,
                        principalTable: "AppointmentCancellations",
                        principalColumn: "AppointmentId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppointmentMailOutbox_AppointmentCheckIns_CheckInAppointmen~",
                        column: x => x.CheckInAppointmentId,
                        principalTable: "AppointmentCheckIns",
                        principalColumn: "AppointmentId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppointmentMailOutbox_IntakeAppointments_AppointmentId",
                        column: x => x.AppointmentId,
                        principalTable: "IntakeAppointments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentMailOutbox_AppointmentId_Kind",
                table: "AppointmentMailOutbox",
                columns: ["AppointmentId", "Kind"],
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentMailOutbox_CancellationAppointmentId",
                table: "AppointmentMailOutbox",
                column: "CancellationAppointmentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentMailOutbox_CheckInAppointmentId",
                table: "AppointmentMailOutbox",
                column: "CheckInAppointmentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentMailOutbox_NextDeliveryAt_Id",
                table: "AppointmentMailOutbox",
                columns: ["NextDeliveryAt", "Id"]);

            // Copy every delivery receipt before replacing the legacy tables and their guards.
            migrationBuilder.Sql("""
                INSERT INTO public."AppointmentMailOutbox" ("Id", "AppointmentId", "Kind", "RecipientEmail", "DeliveryAttempts", "NextDeliveryAt", "DeliveryClaimId", "DeliveryClaimExpiresAt", "DeliveredAt")
                SELECT gen_random_uuid(), "Id", 'Confirmation', "RecipientEmail", "DeliveryAttempts", "NextDeliveryAt", "DeliveryClaimId", "DeliveryClaimExpiresAt", "DeliveredAt" FROM public."AppointmentConfirmations";

                INSERT INTO public."AppointmentMailOutbox" ("Id", "AppointmentId", "Kind", "RecipientEmail", "DeliveryAttempts", "NextDeliveryAt", "DeliveryClaimId", "DeliveryClaimExpiresAt", "DeliveredAt")
                SELECT gen_random_uuid(), "Id", 'Cancellation', "RecipientEmail", "DeliveryAttempts", "NextDeliveryAt", "DeliveryClaimId", "DeliveryClaimExpiresAt", "DeliveredAt" FROM public."AppointmentCancellationMails";

                INSERT INTO public."AppointmentMailOutbox" ("Id", "AppointmentId", "Kind", "RecipientEmail", "DeliveryAttempts", "NextDeliveryAt", "DeliveryClaimId", "DeliveryClaimExpiresAt", "DeliveredAt")
                SELECT gen_random_uuid(), "Id", 'CheckIn', "RecipientEmail", "DeliveryAttempts", "NextDeliveryAt", "DeliveryClaimId", "DeliveryClaimExpiresAt", "DeliveredAt" FROM public."AppointmentCheckInMails";

                CREATE OR REPLACE FUNCTION public.require_appointment_bundle() RETURNS trigger
                LANGUAGE plpgsql SET search_path = pg_catalog AS $$
                DECLARE appointment_id uuid;
                BEGIN
                    IF TG_TABLE_NAME = 'IntakeAppointments' THEN
                        appointment_id := NEW."Id";
                    ELSE
                        appointment_id := NEW."AppointmentId";
                    END IF;
                    IF (SELECT count(*) FROM public."SellerValuations" WHERE "AppointmentId" = appointment_id) <> 1
                        OR (SELECT count(*) FROM public."AppointmentPhotos" WHERE "AppointmentId" = appointment_id) <> 4
                        OR (SELECT count(*) FROM public."AppointmentMailOutbox" WHERE "AppointmentId" = appointment_id AND "Kind" = 'Confirmation') <> 1 THEN
                        RAISE EXCEPTION 'An appointment requires one valuation, four photos and durable confirmation intent.'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_IntakeAppointments_Complete';
                    END IF;
                    RETURN NULL;
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

                CREATE OR REPLACE FUNCTION public.require_checkin_bundle() RETURNS trigger
                LANGUAGE plpgsql SET search_path = pg_catalog AS $$
                DECLARE appointment_id uuid;
                BEGIN
                    IF TG_TABLE_NAME = 'Vehicles' THEN
                        appointment_id := NEW."IntakeAppointmentId";
                    ELSE
                        appointment_id := NEW."AppointmentId";
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM public."AppointmentCheckIns" c
                        JOIN public."Vehicles" v ON (v."Id", v."IntakeAppointmentId", v."IntakeOwnerId", v."BranchId") =
                            (c."VehicleId", c."AppointmentId", c."OwnerId", c."BranchId")
                        JOIN public."AppointmentMailOutbox" m ON m."AppointmentId" = c."AppointmentId" AND m."Kind" = 'CheckIn'
                        JOIN public."AspNetUsers" u ON u."Id" = c."OwnerId"
                        WHERE c."AppointmentId" = appointment_id AND (c."Vin", c."MileageKilometres", c."CheckedInAt") =
                            (v."Vin", v."MileageKilometres", v."CreatedAt") AND m."RecipientEmail" = u."Email") THEN
                        RAISE EXCEPTION 'Check-in requires matching vehicle, immutable evidence and durable customer mail.'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_AppointmentCheckIns_Complete';
                    END IF;
                    RETURN NULL;
                END;
                $$;

                CREATE FUNCTION public.protect_appointment_mail() RETURNS trigger
                LANGUAGE plpgsql SET search_path = pg_catalog AS $$
                BEGIN
                    IF TG_OP = 'DELETE' OR
                        (NEW."Id", NEW."AppointmentId", NEW."Kind", NEW."RecipientEmail") IS DISTINCT FROM
                        (OLD."Id", OLD."AppointmentId", OLD."Kind", OLD."RecipientEmail") OR
                        (OLD."DeliveredAt" IS NOT NULL AND NEW."DeliveredAt" IS DISTINCT FROM OLD."DeliveredAt") THEN
                        RAISE EXCEPTION 'Appointment mail identity, recipient and first delivery receipt are immutable.'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_AppointmentMailOutbox_Immutable';
                    END IF;
                    RETURN NEW;
                END;
                $$;
                CREATE TRIGGER protect_appointment_mail BEFORE UPDATE OR DELETE ON public."AppointmentMailOutbox"
                    FOR EACH ROW EXECUTE FUNCTION public.protect_appointment_mail();
                CREATE CONSTRAINT TRIGGER require_appointment_mail_bundle AFTER INSERT ON public."AppointmentMailOutbox"
                    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION public.require_appointment_bundle();

                DO $$
                DECLARE permission record;
                BEGIN
                    FOR permission IN SELECT grantee, privilege_type FROM information_schema.table_privileges
                        WHERE table_schema = 'public' AND table_name IN
                            ('AppointmentConfirmations', 'AppointmentCancellationMails', 'AppointmentCheckInMails')
                          AND grantee <> current_user
                        GROUP BY grantee, privilege_type HAVING count(DISTINCT table_name) = 3 LOOP
                        EXECUTE format('GRANT %s ON TABLE public."AppointmentMailOutbox" TO %s',
                            permission.privilege_type,
                            CASE WHEN permission.grantee = 'PUBLIC' THEN 'PUBLIC' ELSE quote_ident(permission.grantee) END);
                    END LOOP;
                END;
                $$;
                """);

            migrationBuilder.DropTable(
                name: "AppointmentCancellationMails");

            migrationBuilder.DropTable(
                name: "AppointmentCheckInMails");

            migrationBuilder.DropTable(
                name: "AppointmentConfirmations");
            migrationBuilder.Sql("DROP FUNCTION public.protect_appointment_confirmation();");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppointmentCancellationMails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DeliveredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeliveryAttempts = table.Column<int>(type: "integer", nullable: false),
                    DeliveryClaimExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeliveryClaimId = table.Column<Guid>(type: "uuid", nullable: true),
                    NextDeliveryAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RecipientEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false)
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

            migrationBuilder.CreateTable(
                name: "AppointmentCheckInMails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DeliveredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeliveryAttempts = table.Column<int>(type: "integer", nullable: false),
                    DeliveryClaimExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeliveryClaimId = table.Column<Guid>(type: "uuid", nullable: true),
                    NextDeliveryAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RecipientEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppointmentCheckInMails", x => x.Id);
                    table.CheckConstraint("CK_AppointmentCheckInMails_Shape", "\"RecipientEmail\" ~ '[^[:space:]]' AND \"DeliveryAttempts\" >= 0");
                    table.ForeignKey(
                        name: "FK_AppointmentCheckInMails_AppointmentCheckIns_Id",
                        column: x => x.Id,
                        principalTable: "AppointmentCheckIns",
                        principalColumn: "AppointmentId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppointmentConfirmations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DeliveredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeliveryAttempts = table.Column<int>(type: "integer", nullable: false),
                    DeliveryClaimExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeliveryClaimId = table.Column<Guid>(type: "uuid", nullable: true),
                    NextDeliveryAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RecipientEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppointmentConfirmations", x => x.Id);
                    table.CheckConstraint("CK_AppointmentConfirmations_Shape", "\"RecipientEmail\" ~ '[^[:space:]]' AND \"DeliveryAttempts\" >= 0");
                    table.ForeignKey(
                        name: "FK_AppointmentConfirmations_IntakeAppointments_Id",
                        column: x => x.Id,
                        principalTable: "IntakeAppointments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentCancellationMails_NextDeliveryAt_Id",
                table: "AppointmentCancellationMails",
                columns: ["NextDeliveryAt", "Id"]);

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentCheckInMails_NextDeliveryAt_Id",
                table: "AppointmentCheckInMails",
                columns: ["NextDeliveryAt", "Id"]);

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentConfirmations_NextDeliveryAt_Id",
                table: "AppointmentConfirmations",
                columns: ["NextDeliveryAt", "Id"]);

            migrationBuilder.Sql("""
                INSERT INTO public."AppointmentConfirmations" ("Id", "RecipientEmail", "DeliveryAttempts", "NextDeliveryAt", "DeliveryClaimId", "DeliveryClaimExpiresAt", "DeliveredAt")
                SELECT "AppointmentId", "RecipientEmail", "DeliveryAttempts", "NextDeliveryAt", "DeliveryClaimId", "DeliveryClaimExpiresAt", "DeliveredAt" FROM public."AppointmentMailOutbox" WHERE "Kind" = 'Confirmation';

                INSERT INTO public."AppointmentCancellationMails" ("Id", "RecipientEmail", "DeliveryAttempts", "NextDeliveryAt", "DeliveryClaimId", "DeliveryClaimExpiresAt", "DeliveredAt")
                SELECT "AppointmentId", "RecipientEmail", "DeliveryAttempts", "NextDeliveryAt", "DeliveryClaimId", "DeliveryClaimExpiresAt", "DeliveredAt" FROM public."AppointmentMailOutbox" WHERE "Kind" = 'Cancellation';

                INSERT INTO public."AppointmentCheckInMails" ("Id", "RecipientEmail", "DeliveryAttempts", "NextDeliveryAt", "DeliveryClaimId", "DeliveryClaimExpiresAt", "DeliveredAt")
                SELECT "AppointmentId", "RecipientEmail", "DeliveryAttempts", "NextDeliveryAt", "DeliveryClaimId", "DeliveryClaimExpiresAt", "DeliveredAt" FROM public."AppointmentMailOutbox" WHERE "Kind" = 'CheckIn';

                CREATE OR REPLACE FUNCTION public.require_appointment_bundle() RETURNS trigger
                LANGUAGE plpgsql SET search_path = pg_catalog AS $$
                DECLARE appointment_id uuid;
                BEGIN
                    IF TG_TABLE_NAME = 'IntakeAppointments' OR TG_TABLE_NAME = 'AppointmentConfirmations' THEN
                        appointment_id := NEW."Id";
                    ELSE
                        appointment_id := NEW."AppointmentId";
                    END IF;
                    IF (SELECT count(*) FROM public."SellerValuations" WHERE "AppointmentId" = appointment_id) <> 1
                        OR (SELECT count(*) FROM public."AppointmentPhotos" WHERE "AppointmentId" = appointment_id) <> 4
                        OR (SELECT count(*) FROM public."AppointmentConfirmations" WHERE "Id" = appointment_id) <> 1 THEN
                        RAISE EXCEPTION 'An appointment requires one valuation, four photos and durable confirmation intent.'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_IntakeAppointments_Complete';
                    END IF;
                    RETURN NULL;
                END;
                $$;

                CREATE OR REPLACE FUNCTION public.require_cancellation_mail() RETURNS trigger
                LANGUAGE plpgsql SET search_path = pg_catalog AS $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM public."AppointmentCancellationMails" WHERE "Id" = NEW."AppointmentId") THEN
                        RAISE EXCEPTION 'Cancellation requires durable mail intent.'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_AppointmentCancellations_Mail';
                    END IF;
                    RETURN NULL;
                END;
                $$;

                CREATE OR REPLACE FUNCTION public.require_checkin_bundle() RETURNS trigger
                LANGUAGE plpgsql SET search_path = pg_catalog AS $$
                DECLARE appointment_id uuid;
                BEGIN
                    IF TG_TABLE_NAME = 'Vehicles' THEN
                        appointment_id := NEW."IntakeAppointmentId";
                    ELSE
                        appointment_id := NEW."AppointmentId";
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM public."AppointmentCheckIns" c
                        JOIN public."Vehicles" v ON (v."Id", v."IntakeAppointmentId", v."IntakeOwnerId", v."BranchId") =
                            (c."VehicleId", c."AppointmentId", c."OwnerId", c."BranchId")
                        JOIN public."AppointmentCheckInMails" m ON m."Id" = c."AppointmentId"
                        JOIN public."AspNetUsers" u ON u."Id" = c."OwnerId"
                        WHERE c."AppointmentId" = appointment_id AND (c."Vin", c."MileageKilometres", c."CheckedInAt") =
                            (v."Vin", v."MileageKilometres", v."CreatedAt") AND m."RecipientEmail" = u."Email") THEN
                        RAISE EXCEPTION 'Check-in requires matching vehicle, immutable evidence and durable customer mail.'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_AppointmentCheckIns_Complete';
                    END IF;
                    RETURN NULL;
                END;
                $$;

                CREATE CONSTRAINT TRIGGER require_confirmation_bundle AFTER INSERT ON public."AppointmentConfirmations"
                    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION public.require_appointment_bundle();
                CREATE FUNCTION public.protect_appointment_confirmation() RETURNS trigger
                LANGUAGE plpgsql SET search_path = pg_catalog AS $$
                BEGIN
                    IF (NEW."Id", NEW."RecipientEmail") IS DISTINCT FROM (OLD."Id", OLD."RecipientEmail")
                        OR (OLD."DeliveredAt" IS NOT NULL AND NEW."DeliveredAt" IS DISTINCT FROM OLD."DeliveredAt") THEN
                        RAISE EXCEPTION 'Confirmation identity and first delivery receipt are immutable.'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_AppointmentConfirmations_Immutable';
                    END IF;
                    RETURN NEW;
                END;
                $$;
                CREATE TRIGGER protect_appointment_confirmation BEFORE UPDATE ON public."AppointmentConfirmations"
                    FOR EACH ROW EXECUTE FUNCTION public.protect_appointment_confirmation();
                CREATE TRIGGER protect_cancellation_mail BEFORE UPDATE ON public."AppointmentCancellationMails"
                    FOR EACH ROW EXECUTE FUNCTION public.protect_appointment_confirmation();
                CREATE TRIGGER protect_checkin_mail BEFORE UPDATE ON public."AppointmentCheckInMails"
                    FOR EACH ROW EXECUTE FUNCTION public.protect_appointment_confirmation();

                DO $$
                DECLARE permission record;
                BEGIN
                    FOR permission IN SELECT grantee, privilege_type FROM information_schema.table_privileges
                        WHERE table_schema = 'public' AND table_name = 'AppointmentMailOutbox'
                          AND grantee <> current_user LOOP
                        EXECUTE format('GRANT %s ON TABLE public."AppointmentConfirmations", public."AppointmentCancellationMails", public."AppointmentCheckInMails" TO %s',
                            permission.privilege_type,
                            CASE WHEN permission.grantee = 'PUBLIC' THEN 'PUBLIC' ELSE quote_ident(permission.grantee) END);
                    END LOOP;
                END;
                $$;
                """);

            migrationBuilder.DropTable(name: "AppointmentMailOutbox");
            migrationBuilder.Sql("DROP FUNCTION public.protect_appointment_mail();");
        }
    }
}

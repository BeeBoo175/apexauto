using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dealership.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class IntakeBookings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_IntakePhotos_Id_OwnerId_Angle",
                table: "IntakePhotos",
                columns: ["Id", "OwnerId", "Angle"]);

            migrationBuilder.CreateTable(
                name: "IntakeAppointments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    RegistrationPlate = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntakeAppointments", x => x.Id);
                    table.UniqueConstraint("AK_IntakeAppointments_Id_OwnerId", x => new { x.Id, x.OwnerId });
                    table.CheckConstraint("CK_IntakeAppointments_Shape", "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"RegistrationPlate\" ~ '[^[:space:]]' AND \"StartsAt\" > \"CreatedAt\" AND \"EndsAt\" > \"StartsAt\"");
                    table.ForeignKey(
                        name: "FK_IntakeAppointments_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IntakeAppointments_CustomerProfiles_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "CustomerProfiles",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppointmentConfirmations",
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
                    table.PrimaryKey("PK_AppointmentConfirmations", x => x.Id);
                    table.CheckConstraint("CK_AppointmentConfirmations_Shape", "\"RecipientEmail\" ~ '[^[:space:]]' AND \"DeliveryAttempts\" >= 0");
                    table.ForeignKey(
                        name: "FK_AppointmentConfirmations_IntakeAppointments_Id",
                        column: x => x.Id,
                        principalTable: "IntakeAppointments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppointmentPhotos",
                columns: table => new
                {
                    AppointmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Angle = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    PhotoId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppointmentPhotos", x => new { x.AppointmentId, x.Angle });
                    table.CheckConstraint("CK_AppointmentPhotos_Angle", "\"Angle\" IN ('Driver', 'Passenger', 'Front', 'Rear')");
                    table.ForeignKey(
                        name: "FK_AppointmentPhotos_IntakeAppointments_AppointmentId_OwnerId",
                        columns: x => new { x.AppointmentId, x.OwnerId },
                        principalTable: "IntakeAppointments",
                        principalColumns: ["Id", "OwnerId"],
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppointmentPhotos_IntakePhotos_PhotoId_OwnerId_Angle",
                        columns: x => new { x.PhotoId, x.OwnerId, x.Angle },
                        principalTable: "IntakePhotos",
                        principalColumns: ["Id", "OwnerId", "Angle"],
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SellerValuations",
                columns: table => new
                {
                    AppointmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Make = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Trim = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ManufactureYear = table.Column<int>(type: "integer", nullable: false),
                    MileageKilometres = table.Column<int>(type: "integer", nullable: false),
                    Minimum = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Maximum = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    ValuationYear = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SellerValuations", x => new { x.AppointmentId, x.OwnerId });
                    table.CheckConstraint("CK_SellerValuations_Shape", "\"Make\" ~ '[^[:space:]]' AND \"Model\" ~ '[^[:space:]]' AND \"Trim\" ~ '[^[:space:]]' AND \"ManufactureYear\" > 0 AND \"ManufactureYear\" <= \"ValuationYear\" AND \"MileageKilometres\" >= 0 AND \"Minimum\" >= 0 AND \"Maximum\" >= \"Minimum\" AND \"Currency\" = 'MYR'");
                    table.ForeignKey(
                        name: "FK_SellerValuations_IntakeAppointments_AppointmentId_OwnerId",
                        columns: x => new { x.AppointmentId, x.OwnerId },
                        principalTable: "IntakeAppointments",
                        principalColumns: ["Id", "OwnerId"],
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentConfirmations_NextDeliveryAt_Id",
                table: "AppointmentConfirmations",
                columns: ["NextDeliveryAt", "Id"]);

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentPhotos_AppointmentId_OwnerId",
                table: "AppointmentPhotos",
                columns: ["AppointmentId", "OwnerId"]);

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentPhotos_PhotoId_OwnerId_Angle",
                table: "AppointmentPhotos",
                columns: ["PhotoId", "OwnerId", "Angle"],
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntakeAppointments_BranchId_StartsAt",
                table: "IntakeAppointments",
                columns: ["BranchId", "StartsAt"],
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntakeAppointments_OwnerId_CreatedAt",
                table: "IntakeAppointments",
                columns: ["OwnerId", "CreatedAt"]);

            migrationBuilder.Sql("""
                CREATE FUNCTION public.protect_appointment_slot() RETURNS trigger
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
                CREATE TRIGGER protect_appointment_slot BEFORE INSERT ON public."IntakeAppointments"
                    FOR EACH ROW EXECUTE FUNCTION public.protect_appointment_slot();

                CREATE FUNCTION public.protect_appointment_evidence() RETURNS trigger
                LANGUAGE plpgsql SET search_path = pg_catalog AS $$
                BEGIN
                    RAISE EXCEPTION 'Appointment identity, valuation and photo evidence are immutable.'
                        USING ERRCODE = '23514', CONSTRAINT = 'CK_IntakeAppointments_Immutable';
                END;
                $$;
                CREATE TRIGGER protect_appointment BEFORE UPDATE OR DELETE ON public."IntakeAppointments"
                    FOR EACH ROW EXECUTE FUNCTION public.protect_appointment_evidence();
                CREATE TRIGGER protect_valuation BEFORE UPDATE OR DELETE ON public."SellerValuations"
                    FOR EACH ROW EXECUTE FUNCTION public.protect_appointment_evidence();
                CREATE TRIGGER protect_appointment_photo BEFORE UPDATE OR DELETE ON public."AppointmentPhotos"
                    FOR EACH ROW EXECUTE FUNCTION public.protect_appointment_evidence();

                CREATE FUNCTION public.require_appointment_bundle() RETURNS trigger
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
                CREATE CONSTRAINT TRIGGER require_appointment_bundle AFTER INSERT ON public."IntakeAppointments"
                    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION public.require_appointment_bundle();
                CREATE CONSTRAINT TRIGGER require_valuation_bundle AFTER INSERT ON public."SellerValuations"
                    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION public.require_appointment_bundle();
                CREATE CONSTRAINT TRIGGER require_photo_bundle AFTER INSERT ON public."AppointmentPhotos"
                    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION public.require_appointment_bundle();
                CREATE CONSTRAINT TRIGGER require_confirmation_bundle AFTER INSERT ON public."AppointmentConfirmations"
                    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION public.require_appointment_bundle();

                CREATE FUNCTION public.require_available_appointment_photo() RETURNS trigger
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
                CREATE TRIGGER require_available_appointment_photo BEFORE INSERT ON public."AppointmentPhotos"
                    FOR EACH ROW EXECUTE FUNCTION public.require_available_appointment_photo();

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
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppointmentConfirmations");

            migrationBuilder.DropTable(
                name: "AppointmentPhotos");

            migrationBuilder.DropTable(
                name: "SellerValuations");

            migrationBuilder.DropTable(
                name: "IntakeAppointments");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_IntakePhotos_Id_OwnerId_Angle",
                table: "IntakePhotos");

            migrationBuilder.Sql("""
                DROP FUNCTION public.protect_appointment_evidence();
                DROP FUNCTION public.require_appointment_bundle();
                DROP FUNCTION public.require_available_appointment_photo();
                DROP FUNCTION public.protect_appointment_confirmation();
                DROP FUNCTION public.protect_appointment_slot();
                """);
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dealership.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EmbedAppointmentValuation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                LOCK TABLE public."IntakeAppointments", public."SellerValuations" IN ACCESS EXCLUSIVE MODE;
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM public."IntakeAppointments" a
                        LEFT JOIN public."SellerValuations" v ON (v."AppointmentId", v."OwnerId") = (a."Id", a."OwnerId")
                        WHERE v."AppointmentId" IS NULL) THEN
                        RAISE EXCEPTION 'Every appointment must retain its original owned valuation before consolidation.'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_IntakeAppointments_ValuationSource';
                    END IF;
                END;
                $$;
                DROP TRIGGER protect_appointment ON public."IntakeAppointments";
                """);

            migrationBuilder.AddColumn<string>(
                name: "ValuationCurrency",
                table: "IntakeAppointments",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ValuationMake",
                table: "IntakeAppointments",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ValuationManufactureYear",
                table: "IntakeAppointments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ValuationMaximum",
                table: "IntakeAppointments",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ValuationMileageKilometres",
                table: "IntakeAppointments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ValuationMinimum",
                table: "IntakeAppointments",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ValuationModel",
                table: "IntakeAppointments",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ValuationTrim",
                table: "IntakeAppointments",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ValuationYear",
                table: "IntakeAppointments",
                type: "integer",
                nullable: true);

            // Backfill under migration-owned locks; restore immutability before releasing this transaction.
            migrationBuilder.Sql("""
                UPDATE public."IntakeAppointments" a SET
                    "ValuationMake" = v."Make",
                    "ValuationModel" = v."Model",
                    "ValuationTrim" = v."Trim",
                    "ValuationManufactureYear" = v."ManufactureYear",
                    "ValuationMileageKilometres" = v."MileageKilometres",
                    "ValuationMinimum" = v."Minimum",
                    "ValuationMaximum" = v."Maximum",
                    "ValuationCurrency" = v."Currency",
                    "ValuationYear" = v."ValuationYear"
                FROM public."SellerValuations" v WHERE (v."AppointmentId", v."OwnerId") = (a."Id", a."OwnerId");
                CREATE TRIGGER protect_appointment BEFORE UPDATE OR DELETE ON public."IntakeAppointments"
                    FOR EACH ROW EXECUTE FUNCTION public.protect_appointment_evidence();
                ALTER TABLE public."IntakeAppointments"
                    ALTER COLUMN "ValuationMake" SET NOT NULL,
                    ALTER COLUMN "ValuationModel" SET NOT NULL,
                    ALTER COLUMN "ValuationTrim" SET NOT NULL,
                    ALTER COLUMN "ValuationManufactureYear" SET NOT NULL,
                    ALTER COLUMN "ValuationMileageKilometres" SET NOT NULL,
                    ALTER COLUMN "ValuationMinimum" SET NOT NULL,
                    ALTER COLUMN "ValuationMaximum" SET NOT NULL,
                    ALTER COLUMN "ValuationCurrency" SET NOT NULL,
                    ALTER COLUMN "ValuationYear" SET NOT NULL;

                CREATE OR REPLACE FUNCTION public.require_appointment_bundle() RETURNS trigger
                LANGUAGE plpgsql SET search_path = pg_catalog AS $$
                DECLARE appointment_id uuid;
                BEGIN
                    IF TG_TABLE_NAME = 'IntakeAppointments' THEN
                        appointment_id := NEW."Id";
                    ELSE
                        appointment_id := NEW."AppointmentId";
                    END IF;
                    IF (SELECT count(*) FROM public."AppointmentPhotos" WHERE "AppointmentId" = appointment_id) <> 4
                        OR (SELECT count(*) FROM public."AppointmentMailOutbox" WHERE "AppointmentId" = appointment_id AND "Kind" = 'Confirmation') <> 1 THEN
                        RAISE EXCEPTION 'An appointment requires one valuation, four photos and durable confirmation intent.'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_IntakeAppointments_Complete';
                    END IF;
                    RETURN NULL;
                END;
                $$;

                CREATE OR REPLACE FUNCTION public.validate_vehicle_intake() RETURNS trigger
                LANGUAGE plpgsql SET search_path = pg_catalog AS $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM public."IntakeAppointments" a
                        WHERE a."Id" = NEW."IntakeAppointmentId" AND a."OwnerId" = NEW."IntakeOwnerId"
                          AND a."BranchId" = NEW."BranchId" AND a."RegistrationPlate" = NEW."RegistrationPlate"
                          AND (a."ValuationMake", a."ValuationModel", a."ValuationTrim", a."ValuationManufactureYear") =
                              (NEW."Make", NEW."Model", NEW."Trim", NEW."ManufactureYear"))
                        OR NEW."RegistrationIdentity" <> translate(upper(NEW."RegistrationPlate"),
                            U&'\0009\000A\000B\000C\000D\0020\0085\00A0\1680\2000\2001\2002\2003\2004\2005\2006\2007\2008\2009\200A\2028\2029\202F\205F\3000', '') THEN
                        RAISE EXCEPTION 'Vehicle intake must match its booked vehicle and registration identity.'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_Vehicles_Provenance';
                    END IF;
                    RETURN NEW;
                END;
                $$;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_IntakeAppointments_Valuation",
                table: "IntakeAppointments",
                sql: "\"ValuationMake\" ~ '[^[:space:]]' AND \"ValuationModel\" ~ '[^[:space:]]' AND \"ValuationTrim\" ~ '[^[:space:]]' AND \"ValuationManufactureYear\" > 0 AND \"ValuationManufactureYear\" <= \"ValuationYear\" AND \"ValuationMileageKilometres\" >= 0 AND \"ValuationMinimum\" >= 0 AND \"ValuationMaximum\" >= \"ValuationMinimum\" AND \"ValuationCurrency\" = 'MYR'");

            migrationBuilder.DropTable(name: "SellerValuations");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SellerValuations",
                columns: table => new
                {
                    AppointmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Make = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    ManufactureYear = table.Column<int>(type: "integer", nullable: false),
                    Maximum = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    MileageKilometres = table.Column<int>(type: "integer", nullable: false),
                    Minimum = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Trim = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
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

            migrationBuilder.Sql("""
                INSERT INTO public."SellerValuations" ("AppointmentId", "OwnerId", "Make", "Model", "Trim", "ManufactureYear", "MileageKilometres", "Minimum", "Maximum", "Currency", "ValuationYear")
                SELECT "Id", "OwnerId", "ValuationMake", "ValuationModel", "ValuationTrim", "ValuationManufactureYear", "ValuationMileageKilometres", "ValuationMinimum", "ValuationMaximum", "ValuationCurrency", "ValuationYear" FROM public."IntakeAppointments";
                CREATE TRIGGER protect_valuation BEFORE UPDATE OR DELETE ON public."SellerValuations"
                    FOR EACH ROW EXECUTE FUNCTION public.protect_appointment_evidence();
                CREATE CONSTRAINT TRIGGER require_valuation_bundle AFTER INSERT ON public."SellerValuations"
                    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION public.require_appointment_bundle();

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

                CREATE OR REPLACE FUNCTION public.validate_vehicle_intake() RETURNS trigger
                LANGUAGE plpgsql SET search_path = pg_catalog AS $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM public."IntakeAppointments" a
                        JOIN public."SellerValuations" v ON v."AppointmentId" = a."Id"
                        WHERE a."Id" = NEW."IntakeAppointmentId" AND a."OwnerId" = NEW."IntakeOwnerId"
                          AND a."BranchId" = NEW."BranchId" AND a."RegistrationPlate" = NEW."RegistrationPlate"
                          AND (v."Make", v."Model", v."Trim", v."ManufactureYear") =
                              (NEW."Make", NEW."Model", NEW."Trim", NEW."ManufactureYear"))
                        OR NEW."RegistrationIdentity" <> translate(upper(NEW."RegistrationPlate"),
                            U&'\0009\000A\000B\000C\000D\0020\0085\00A0\1680\2000\2001\2002\2003\2004\2005\2006\2007\2008\2009\200A\2028\2029\202F\205F\3000', '') THEN
                        RAISE EXCEPTION 'Vehicle intake must match its booked vehicle and registration identity.'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_Vehicles_Provenance';
                    END IF;
                    RETURN NEW;
                END;
                $$;

                DO $$
                DECLARE permission record;
                BEGIN
                    FOR permission IN SELECT grantee, privilege_type FROM information_schema.table_privileges
                        WHERE table_schema = 'public' AND table_name = 'IntakeAppointments'
                          AND grantee <> current_user LOOP
                        EXECUTE format('GRANT %s ON TABLE public."SellerValuations" TO %s', permission.privilege_type,
                            CASE WHEN permission.grantee = 'PUBLIC' THEN 'PUBLIC' ELSE quote_ident(permission.grantee) END);
                    END LOOP;
                END;
                $$;
                """);

            migrationBuilder.DropCheckConstraint(
                name: "CK_IntakeAppointments_Valuation",
                table: "IntakeAppointments");

            migrationBuilder.DropColumn(
                name: "ValuationCurrency",
                table: "IntakeAppointments");

            migrationBuilder.DropColumn(
                name: "ValuationMake",
                table: "IntakeAppointments");

            migrationBuilder.DropColumn(
                name: "ValuationManufactureYear",
                table: "IntakeAppointments");

            migrationBuilder.DropColumn(
                name: "ValuationMaximum",
                table: "IntakeAppointments");

            migrationBuilder.DropColumn(
                name: "ValuationMileageKilometres",
                table: "IntakeAppointments");

            migrationBuilder.DropColumn(
                name: "ValuationMinimum",
                table: "IntakeAppointments");

            migrationBuilder.DropColumn(
                name: "ValuationModel",
                table: "IntakeAppointments");

            migrationBuilder.DropColumn(
                name: "ValuationTrim",
                table: "IntakeAppointments");

            migrationBuilder.DropColumn(
                name: "ValuationYear",
                table: "IntakeAppointments");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dealership.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AppointmentCheckIns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_IntakeAppointments_Id_OwnerId_BranchId",
                table: "IntakeAppointments",
                columns: ["Id", "OwnerId", "BranchId"]);

            migrationBuilder.CreateTable(
                name: "Vehicles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IntakeAppointmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    IntakeOwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Vin = table.Column<string>(type: "character varying(17)", maxLength: 17, nullable: false),
                    RegistrationPlate = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RegistrationIdentity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Make = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Trim = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ManufactureYear = table.Column<int>(type: "integer", nullable: false),
                    MileageKilometres = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vehicles", x => x.Id);
                    table.UniqueConstraint("AK_Vehicles_Id_IntakeAppointmentId_IntakeOwnerId_BranchId", x => new { x.Id, x.IntakeAppointmentId, x.IntakeOwnerId, x.BranchId });
                    table.CheckConstraint("CK_Vehicles_Identity", "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"Vin\" ~ '^[A-HJ-NPR-Z0-9]{17}$' AND \"RegistrationIdentity\" ~ '[^[:space:]]'");
                    table.CheckConstraint("CK_Vehicles_State", "\"MileageKilometres\" >= 0 AND \"Status\" = 'PendingInspection' AND \"ManufactureYear\" > 0");
                    table.ForeignKey(
                        name: "FK_Vehicles_IntakeAppointments_IntakeAppointmentId_IntakeOwner~",
                        columns: x => new { x.IntakeAppointmentId, x.IntakeOwnerId, x.BranchId },
                        principalTable: "IntakeAppointments",
                        principalColumns: ["Id", "OwnerId", "BranchId"],
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppointmentCheckIns",
                columns: table => new
                {
                    AppointmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uuid", nullable: false),
                    StaffId = table.Column<Guid>(type: "uuid", nullable: false),
                    CheckedInAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Vin = table.Column<string>(type: "character varying(17)", maxLength: 17, nullable: false),
                    MileageKilometres = table.Column<int>(type: "integer", nullable: false),
                    CustomerPresent = table.Column<bool>(type: "boolean", nullable: false),
                    IdentityConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    OwnershipConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    VehicleConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppointmentCheckIns", x => x.AppointmentId);
                    table.CheckConstraint("CK_AppointmentCheckIns_Notes", "\"Notes\" IS NULL OR (\"Notes\" ~ '[^[:space:]]' AND length(\"Notes\") <= 1000)");
                    table.CheckConstraint("CK_AppointmentCheckIns_Physical", "\"CustomerPresent\" AND \"IdentityConfirmed\" AND \"OwnershipConfirmed\" AND \"VehicleConfirmed\" AND \"MileageKilometres\" >= 0 AND \"Vin\" ~ '^[A-HJ-NPR-Z0-9]{17}$'");
                    table.ForeignKey(
                        name: "FK_AppointmentCheckIns_AspNetUsers_StaffId",
                        column: x => x.StaffId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppointmentCheckIns_IntakeAppointments_AppointmentId_OwnerI~",
                        columns: x => new { x.AppointmentId, x.OwnerId, x.BranchId },
                        principalTable: "IntakeAppointments",
                        principalColumns: ["Id", "OwnerId", "BranchId"],
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppointmentCheckIns_Vehicles_VehicleId_AppointmentId_OwnerI~",
                        columns: x => new { x.VehicleId, x.AppointmentId, x.OwnerId, x.BranchId },
                        principalTable: "Vehicles",
                        principalColumns: ["Id", "IntakeAppointmentId", "IntakeOwnerId", "BranchId"],
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppointmentCheckInMails",
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
                    table.PrimaryKey("PK_AppointmentCheckInMails", x => x.Id);
                    table.CheckConstraint("CK_AppointmentCheckInMails_Shape", "\"RecipientEmail\" ~ '[^[:space:]]' AND \"DeliveryAttempts\" >= 0");
                    table.ForeignKey(
                        name: "FK_AppointmentCheckInMails_AppointmentCheckIns_Id",
                        column: x => x.Id,
                        principalTable: "AppointmentCheckIns",
                        principalColumn: "AppointmentId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentCheckInMails_NextDeliveryAt_Id",
                table: "AppointmentCheckInMails",
                columns: ["NextDeliveryAt", "Id"]);

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentCheckIns_AppointmentId_OwnerId_BranchId",
                table: "AppointmentCheckIns",
                columns: ["AppointmentId", "OwnerId", "BranchId"],
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentCheckIns_BranchId_CheckedInAt",
                table: "AppointmentCheckIns",
                columns: ["BranchId", "CheckedInAt"]);

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentCheckIns_StaffId",
                table: "AppointmentCheckIns",
                column: "StaffId");

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentCheckIns_VehicleId_AppointmentId_OwnerId_BranchId",
                table: "AppointmentCheckIns",
                columns: ["VehicleId", "AppointmentId", "OwnerId", "BranchId"],
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_IntakeAppointmentId_IntakeOwnerId_BranchId",
                table: "Vehicles",
                columns: ["IntakeAppointmentId", "IntakeOwnerId", "BranchId"],
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_RegistrationIdentity",
                table: "Vehicles",
                column: "RegistrationIdentity",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_Vin",
                table: "Vehicles",
                column: "Vin",
                unique: true);

            migrationBuilder.Sql("""
                CREATE FUNCTION public.validate_vehicle_intake() RETURNS trigger
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
                CREATE TRIGGER validate_vehicle_intake BEFORE INSERT ON public."Vehicles"
                    FOR EACH ROW EXECUTE FUNCTION public.validate_vehicle_intake();

                CREATE FUNCTION public.validate_appointment_checkin() RETURNS trigger
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
                CREATE TRIGGER validate_appointment_checkin BEFORE INSERT ON public."AppointmentCheckIns"
                    FOR EACH ROW EXECUTE FUNCTION public.validate_appointment_checkin();

                CREATE FUNCTION public.require_checkin_bundle() RETURNS trigger
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
                CREATE CONSTRAINT TRIGGER require_vehicle_checkin AFTER INSERT ON public."Vehicles"
                    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION public.require_checkin_bundle();
                CREATE CONSTRAINT TRIGGER require_checkin_bundle AFTER INSERT ON public."AppointmentCheckIns"
                    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION public.require_checkin_bundle();

                CREATE FUNCTION public.protect_vehicle_provenance() RETURNS trigger
                LANGUAGE plpgsql SET search_path = pg_catalog AS $$
                BEGIN
                    IF (NEW."Id", NEW."IntakeAppointmentId", NEW."IntakeOwnerId", NEW."BranchId", NEW."Vin",
                        NEW."RegistrationPlate", NEW."RegistrationIdentity", NEW."Make", NEW."Model", NEW."Trim", NEW."ManufactureYear", NEW."CreatedAt")
                        IS DISTINCT FROM
                        (OLD."Id", OLD."IntakeAppointmentId", OLD."IntakeOwnerId", OLD."BranchId", OLD."Vin",
                        OLD."RegistrationPlate", OLD."RegistrationIdentity", OLD."Make", OLD."Model", OLD."Trim", OLD."ManufactureYear", OLD."CreatedAt") THEN
                        RAISE EXCEPTION 'Vehicle intake provenance is immutable.'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_Vehicles_Immutable';
                    END IF;
                    RETURN NEW;
                END;
                $$;
                CREATE TRIGGER protect_vehicle_provenance BEFORE UPDATE ON public."Vehicles"
                    FOR EACH ROW EXECUTE FUNCTION public.protect_vehicle_provenance();
                CREATE TRIGGER protect_vehicle_history BEFORE DELETE ON public."Vehicles"
                    FOR EACH ROW EXECUTE FUNCTION public.protect_appointment_evidence();
                CREATE TRIGGER protect_checkin_evidence BEFORE UPDATE OR DELETE ON public."AppointmentCheckIns"
                    FOR EACH ROW EXECUTE FUNCTION public.protect_appointment_evidence();
                CREATE TRIGGER protect_checkin_mail BEFORE UPDATE ON public."AppointmentCheckInMails"
                    FOR EACH ROW EXECUTE FUNCTION public.protect_appointment_confirmation();

                CREATE FUNCTION public.prevent_checked_in_cancellation() RETURNS trigger
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
                CREATE TRIGGER prevent_checked_in_cancellation BEFORE INSERT ON public."AppointmentCancellations"
                    FOR EACH ROW EXECUTE FUNCTION public.prevent_checked_in_cancellation();

                CREATE FUNCTION public.prevent_checked_in_photo_removal() RETURNS trigger
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
                CREATE TRIGGER prevent_checked_in_photo_removal BEFORE INSERT ON public."PhotoRemovals"
                    FOR EACH ROW EXECUTE FUNCTION public.prevent_checked_in_photo_removal();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP FUNCTION public.prevent_checked_in_photo_removal() CASCADE;
                DROP FUNCTION public.prevent_checked_in_cancellation() CASCADE;
                DROP FUNCTION public.protect_vehicle_provenance() CASCADE;
                DROP FUNCTION public.require_checkin_bundle() CASCADE;
                DROP FUNCTION public.validate_appointment_checkin() CASCADE;
                DROP FUNCTION public.validate_vehicle_intake() CASCADE;
                """);

            migrationBuilder.DropTable(
                name: "AppointmentCheckInMails");

            migrationBuilder.DropTable(
                name: "AppointmentCheckIns");

            migrationBuilder.DropTable(
                name: "Vehicles");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_IntakeAppointments_Id_OwnerId_BranchId",
                table: "IntakeAppointments");
        }
    }
}

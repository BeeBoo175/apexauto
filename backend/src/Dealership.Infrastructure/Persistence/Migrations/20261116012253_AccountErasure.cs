using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dealership.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AccountErasure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_TestDriveMailOutbox_Shape",
                table: "TestDriveMailOutbox");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SaleDeliveryMailOutbox_Shape",
                table: "SaleDeliveryMailOutbox");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Reservations_Buyer",
                table: "Reservations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ReservationDepositMailOutbox_Shape",
                table: "ReservationDepositMailOutbox");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PasswordResetChallenges_Identity",
                table: "PasswordResetChallenges");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AppointmentMailOutbox_Shape",
                table: "AppointmentMailOutbox");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AccountEvents_Shape",
                table: "AccountEvents");

            migrationBuilder.AlterColumn<string>(
                name: "RecipientEmail",
                table: "TestDriveMailOutbox",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<string>(
                name: "RecipientEmail",
                table: "SaleDeliveryMailOutbox",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<string>(
                name: "BuyerNric",
                table: "Reservations",
                type: "character varying(12)",
                maxLength: 12,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(12)",
                oldMaxLength: 12);

            migrationBuilder.AlterColumn<string>(
                name: "BuyerMobile",
                table: "Reservations",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30);

            migrationBuilder.AlterColumn<string>(
                name: "BuyerEmail",
                table: "Reservations",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<string>(
                name: "RecipientEmail",
                table: "ReservationDepositMailOutbox",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<string>(
                name: "RecipientEmail",
                table: "PasswordResetChallenges",
                type: "character varying(254)",
                maxLength: 254,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(254)",
                oldMaxLength: 254);

            migrationBuilder.AlterColumn<string>(
                name: "RecipientEmail",
                table: "EmailVerificationChallenges",
                type: "character varying(254)",
                maxLength: 254,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(254)",
                oldMaxLength: 254);

            migrationBuilder.AlterColumn<string>(
                name: "RecipientEmail",
                table: "AppointmentMailOutbox",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "MinimizedAt",
                table: "AccountEvents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_TestDriveMailOutbox_Shape",
                table: "TestDriveMailOutbox",
                sql: "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND (\"RecipientEmail\" IS NULL OR \"RecipientEmail\" ~ '[^[:space:]]') AND \"DeliveryAttempts\" >= 0 AND isfinite(\"NextDeliveryAt\") AND ((\"DeliveryClaimId\" IS NULL) = (\"DeliveryClaimExpiresAt\" IS NULL))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SaleDeliveryMailOutbox_Shape",
                table: "SaleDeliveryMailOutbox",
                sql: "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"DeliveryAttempts\" >= 0 AND (\"RecipientEmail\" IS NULL OR \"RecipientEmail\" ~ '[^[:space:]]') AND isfinite(\"NextDeliveryAt\") AND ((\"DeliveryClaimId\" IS NULL) = (\"DeliveryClaimExpiresAt\" IS NULL))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Reservations_Buyer",
                table: "Reservations",
                sql: "\"BuyerName\" ~ '[^[:space:]]' AND (\"BuyerEmail\" IS NULL OR \"BuyerEmail\" ~ '[^[:space:]]') AND (\"BuyerMobile\" IS NULL OR \"BuyerMobile\" ~ '[^[:space:]]') AND (\"BuyerNric\" IS NULL OR \"BuyerNric\" ~ '^[0-9]{12}$')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ReservationDepositMailOutbox_Shape",
                table: "ReservationDepositMailOutbox",
                sql: "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"DeliveryAttempts\" >= 0 AND (\"RecipientEmail\" IS NULL OR \"RecipientEmail\" ~ '[^[:space:]]')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PasswordResetChallenges_Identity",
                table: "PasswordResetChallenges",
                sql: "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND (\"RecipientEmail\" IS NULL OR \"RecipientEmail\" ~ '[^[:space:]]')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AppointmentMailOutbox_Shape",
                table: "AppointmentMailOutbox",
                sql: "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND (\"RecipientEmail\" IS NULL OR \"RecipientEmail\" ~ '[^[:space:]]') AND \"DeliveryAttempts\" >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_AccountEvents_TargetId",
                table: "AccountEvents",
                column: "TargetId",
                unique: true,
                filter: "\"Kind\" = 'Erased'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AccountEvents_Shape",
                table: "AccountEvents",
                sql: "isfinite(\"RecordedAt\") AND \"Kind\" IN ('UsernameChanged', 'PictureRemoved', 'ProfileEdited', 'EmailChanged', 'BranchTransferred', 'Suspended', 'SuspensionLifted', 'Deactivated', 'Reactivated', 'NricRevealed', 'SessionsRevoked', 'VerificationResent', 'ResetSent', 'Erased') AND (\"Changes\" IS NULL OR jsonb_typeof(\"Changes\") = 'array') AND ((\"MinimizedAt\" IS NOT NULL AND \"Reason\" IS NULL AND \"Changes\" IS NULL) OR (\"MinimizedAt\" IS NULL AND ((\"Kind\" IN ('Suspended', 'Deactivated', 'BranchTransferred', 'NricRevealed', 'SessionsRevoked', 'VerificationResent', 'ResetSent', 'ProfileEdited', 'EmailChanged') AND \"Reason\" IS NOT NULL AND \"Reason\" = btrim(\"Reason\") AND length(\"Reason\") BETWEEN 1 AND 1000 AND \"Reason\" !~ '[[:cntrl:]]') OR (\"Kind\" = 'Erased' AND (\"Reason\" IS NULL OR (\"Reason\" IS NOT NULL AND \"Reason\" = btrim(\"Reason\") AND length(\"Reason\") BETWEEN 1 AND 1000 AND \"Reason\" !~ '[[:cntrl:]]'))) OR (\"Kind\" NOT IN ('Suspended', 'Deactivated', 'BranchTransferred', 'NricRevealed', 'SessionsRevoked', 'VerificationResent', 'ResetSent', 'ProfileEdited', 'EmailChanged', 'Erased') AND \"Reason\" IS NULL))))");

            // Each guard below is a new per-table function: the shared functions stay untouched, and the old
            // functions stay in place so Down() can restore the exact earlier trigger bindings.
            migrationBuilder.Sql("""
                CREATE FUNCTION public.guard_account_event_minimization() RETURNS trigger
                LANGUAGE plpgsql SET search_path = pg_catalog AS $$
                BEGIN
                    IF (to_jsonb(NEW) - 'Reason' - 'Changes' - 'MinimizedAt') =
                        (to_jsonb(OLD) - 'Reason' - 'Changes' - 'MinimizedAt') AND
                        OLD."MinimizedAt" IS NULL AND NEW."MinimizedAt" IS NOT NULL AND
                        NEW."Reason" IS NULL AND NEW."Changes" IS NULL AND
                        EXISTS (SELECT 1 FROM public."AspNetUsers" u WHERE u."Id" = OLD."TargetId" AND u."ClosedAt" IS NOT NULL) THEN
                        RETURN NEW;
                    END IF;
                    RAISE EXCEPTION 'Account history is append-only'
                        USING ERRCODE = '23514', CONSTRAINT = 'CK_AccountEvents_Immutable';
                END $$;
                DROP TRIGGER account_events_append_only ON "AccountEvents";
                CREATE TRIGGER account_events_no_delete BEFORE DELETE ON "AccountEvents"
                    FOR EACH ROW EXECUTE FUNCTION forbid_account_event_change();
                CREATE TRIGGER account_events_minimization BEFORE UPDATE ON "AccountEvents"
                    FOR EACH ROW EXECUTE FUNCTION public.guard_account_event_minimization();
                """);

            migrationBuilder.Sql("""
                CREATE FUNCTION public.guard_reservation_update() RETURNS trigger
                LANGUAGE plpgsql SET search_path = pg_catalog AS $$
                BEGIN
                    IF OLD."IsActive" AND NOT NEW."IsActive" AND
                       (to_jsonb(OLD) - 'IsActive') = (to_jsonb(NEW) - 'IsActive') AND
                       (EXISTS (SELECT 1 FROM public."ReservationReleases" WHERE "ReservationId" = OLD."Id") OR
                           EXISTS (SELECT 1 FROM public."SalesOrders" WHERE "Id" = OLD."Id" AND "Status" = 'Settled')) THEN
                        RETURN NEW;
                    END IF;
                    -- A closed customer's inactive reservation may lose only its contact and NRIC copies.
                    IF NOT OLD."IsActive" AND NOT NEW."IsActive" AND
                       (to_jsonb(NEW) - 'BuyerEmail' - 'BuyerMobile' - 'BuyerNric') =
                           (to_jsonb(OLD) - 'BuyerEmail' - 'BuyerMobile' - 'BuyerNric') AND
                       (NEW."BuyerEmail" IS NULL OR NEW."BuyerEmail" = OLD."BuyerEmail") AND
                       (NEW."BuyerMobile" IS NULL OR NEW."BuyerMobile" = OLD."BuyerMobile") AND
                       (NEW."BuyerNric" IS NULL OR NEW."BuyerNric" = OLD."BuyerNric") AND
                       EXISTS (SELECT 1 FROM public."AspNetUsers" u WHERE u."Id" = OLD."CustomerId" AND u."ClosedAt" IS NOT NULL) THEN
                        RETURN NEW;
                    END IF;
                    RAISE EXCEPTION 'Only a retained release can free an active reservation'
                        USING ERRCODE = '23514', CONSTRAINT = 'CK_Reservations_Immutable';
                END $$;
                DROP TRIGGER reservation_guard ON "Reservations";
                CREATE TRIGGER reservation_guard BEFORE INSERT OR DELETE ON "Reservations"
                    FOR EACH ROW EXECUTE FUNCTION guard_reservation();
                CREATE TRIGGER reservation_update_guard BEFORE UPDATE ON "Reservations"
                    FOR EACH ROW EXECUTE FUNCTION public.guard_reservation_update();
                """);

            migrationBuilder.Sql("""
                CREATE FUNCTION public.protect_password_reset_minimizing() RETURNS trigger
                LANGUAGE plpgsql SET search_path = pg_catalog AS $$
                BEGIN
                    IF (NEW."Id", NEW."UserId", NEW."CreatedAt", NEW."ExpiresAt")
                        IS DISTINCT FROM
                        (OLD."Id", OLD."UserId", OLD."CreatedAt", OLD."ExpiresAt")
                        OR (NEW."RecipientEmail" IS DISTINCT FROM OLD."RecipientEmail" AND NOT
                            (NEW."RecipientEmail" IS NULL AND EXISTS (SELECT 1 FROM public."AspNetUsers" u
                                WHERE u."Id" = OLD."UserId" AND u."ClosedAt" IS NOT NULL)))
                        OR (OLD."ConsumedAt" IS NOT NULL AND
                            (NEW."ConsumedAt", NEW."CompletedSecurityStamp") IS DISTINCT FROM
                            (OLD."ConsumedAt", OLD."CompletedSecurityStamp"))
                        OR (OLD."InvalidatedAt" IS NOT NULL AND
                            NEW."InvalidatedAt" IS DISTINCT FROM OLD."InvalidatedAt") THEN
                        RAISE EXCEPTION 'Reset identity, lifetime and completed receipts are immutable.'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_PasswordResetChallenges_Immutable';
                    END IF;
                    RETURN NEW;
                END;
                $$;
                DROP TRIGGER protect_password_reset ON public."PasswordResetChallenges";
                CREATE TRIGGER protect_password_reset BEFORE UPDATE ON public."PasswordResetChallenges"
                    FOR EACH ROW EXECUTE FUNCTION public.protect_password_reset_minimizing();
                """);

            migrationBuilder.Sql("""
                CREATE FUNCTION public.protect_appointment_mail_minimizing() RETURNS trigger
                LANGUAGE plpgsql SET search_path = pg_catalog AS $$
                BEGIN
                    IF TG_OP = 'DELETE' OR
                        (NEW."Id", NEW."AppointmentId", NEW."Kind") IS DISTINCT FROM
                        (OLD."Id", OLD."AppointmentId", OLD."Kind") OR
                        (NEW."RecipientEmail" IS DISTINCT FROM OLD."RecipientEmail" AND NOT
                            (NEW."RecipientEmail" IS NULL AND EXISTS (SELECT 1 FROM public."IntakeAppointments" a
                                JOIN public."AspNetUsers" u ON u."Id" = a."OwnerId"
                                WHERE a."Id" = OLD."AppointmentId" AND u."ClosedAt" IS NOT NULL))) OR
                        (OLD."DeliveredAt" IS NOT NULL AND NEW."DeliveredAt" IS DISTINCT FROM OLD."DeliveredAt") THEN
                        RAISE EXCEPTION 'Appointment mail identity, recipient and first delivery receipt are immutable.'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_AppointmentMailOutbox_Immutable';
                    END IF;
                    RETURN NEW;
                END;
                $$;
                DROP TRIGGER protect_appointment_mail ON public."AppointmentMailOutbox";
                CREATE TRIGGER protect_appointment_mail BEFORE UPDATE OR DELETE ON public."AppointmentMailOutbox"
                    FOR EACH ROW EXECUTE FUNCTION public.protect_appointment_mail_minimizing();
                """);

            migrationBuilder.Sql("""
                CREATE FUNCTION public.guard_test_drive_mail_minimizing() RETURNS trigger
                LANGUAGE plpgsql SET search_path = pg_catalog AS $$
                BEGIN
                    IF TG_OP = 'DELETE' OR (TG_OP = 'UPDATE' AND (
                        ROW(NEW."Id", NEW."BookingId", NEW."Kind") IS DISTINCT FROM
                        ROW(OLD."Id", OLD."BookingId", OLD."Kind") OR
                        (NEW."RecipientEmail" IS DISTINCT FROM OLD."RecipientEmail" AND NOT
                            (NEW."RecipientEmail" IS NULL AND EXISTS (SELECT 1 FROM public."TestDriveBookings" b
                                JOIN public."AspNetUsers" u ON u."Id" = b."CustomerId"
                                WHERE b."Id" = OLD."BookingId" AND u."ClosedAt" IS NOT NULL))) OR
                        (OLD."DeliveredAt" IS NOT NULL AND NEW."DeliveredAt" IS DISTINCT FROM OLD."DeliveredAt") OR
                        NEW."DeliveryAttempts" < OLD."DeliveryAttempts")) THEN
                        RAISE EXCEPTION 'Test-drive mail identity and first delivery receipt are immutable'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_TestDriveMailOutbox_Immutable';
                    END IF;
                    IF TG_OP = 'INSERT' AND NOT EXISTS (
                        SELECT 1 FROM public."TestDriveBookings" b JOIN public."AspNetUsers" u ON u."Id" = b."CustomerId"
                        WHERE b."Id" = NEW."BookingId" AND u."Email" = NEW."RecipientEmail") THEN
                        RAISE EXCEPTION 'Test-drive mail must retain the booking customer recipient'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_TestDriveMailOutbox_Recipient';
                    END IF;
                    RETURN NEW;
                END $$;
                DROP TRIGGER test_drive_mail_guard ON "TestDriveMailOutbox";
                CREATE TRIGGER test_drive_mail_guard BEFORE INSERT OR UPDATE OR DELETE ON "TestDriveMailOutbox"
                    FOR EACH ROW EXECUTE FUNCTION public.guard_test_drive_mail_minimizing();
                """);

            migrationBuilder.Sql("""
                CREATE FUNCTION public.guard_sale_delivery_mail_minimizing() RETURNS trigger
                LANGUAGE plpgsql SET search_path = pg_catalog AS $$
                BEGIN
                    IF TG_OP = 'DELETE' OR (TG_OP = 'UPDATE' AND (
                        ROW(NEW."Id", NEW."SalesOrderId", NEW."DeliveryOperationId") IS DISTINCT FROM
                        ROW(OLD."Id", OLD."SalesOrderId", OLD."DeliveryOperationId") OR
                        (NEW."RecipientEmail" IS DISTINCT FROM OLD."RecipientEmail" AND NOT
                            (NEW."RecipientEmail" IS NULL AND EXISTS (SELECT 1 FROM public."Reservations" r
                                JOIN public."AspNetUsers" u ON u."Id" = r."CustomerId"
                                WHERE r."Id" = OLD."SalesOrderId" AND u."ClosedAt" IS NOT NULL))))) THEN
                        RAISE EXCEPTION 'Delivery mail retains its original identity and recipient'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_SaleDeliveryMailOutbox_Immutable';
                    END IF;
                    IF TG_OP = 'INSERT' AND NOT EXISTS (
                        SELECT 1 FROM public."SalesOrderEvents" e JOIN public."Reservations" r ON r."Id" = e."SalesOrderId"
                        WHERE e."Id" = NEW."DeliveryOperationId" AND e."SalesOrderId" = NEW."SalesOrderId" AND
                            e."Kind" = 'Delivered' AND NEW."RecipientEmail" = r."BuyerEmail" AND
                            NEW."NextDeliveryAt" = e."RecordedAt" AND NEW."DeliveryAttempts" = 0 AND
                            NEW."DeliveredAt" IS NULL AND NEW."DeliveryClaimId" IS NULL) THEN
                        RAISE EXCEPTION 'Delivery mail requires its delivery receipt and original customer recipient'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_SaleDeliveryMailOutbox_Delivery';
                    END IF;
                    RETURN NEW;
                END $$;
                DROP TRIGGER sale_delivery_mail_guard ON "SaleDeliveryMailOutbox";
                CREATE TRIGGER sale_delivery_mail_guard BEFORE INSERT OR UPDATE OR DELETE ON "SaleDeliveryMailOutbox"
                    FOR EACH ROW EXECUTE FUNCTION public.guard_sale_delivery_mail_minimizing();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restoring NOT NULL and the old checks would need invented personal values, so refuse instead.
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "AccountEvents" WHERE "MinimizedAt" IS NOT NULL OR "Kind" = 'Erased') OR
                        EXISTS (SELECT 1 FROM "Reservations" WHERE "BuyerEmail" IS NULL OR "BuyerMobile" IS NULL OR "BuyerNric" IS NULL) OR
                        EXISTS (SELECT 1 FROM "EmailVerificationChallenges" WHERE "RecipientEmail" IS NULL) OR
                        EXISTS (SELECT 1 FROM "PasswordResetChallenges" WHERE "RecipientEmail" IS NULL) OR
                        EXISTS (SELECT 1 FROM "AppointmentMailOutbox" WHERE "RecipientEmail" IS NULL) OR
                        EXISTS (SELECT 1 FROM "TestDriveMailOutbox" WHERE "RecipientEmail" IS NULL) OR
                        EXISTS (SELECT 1 FROM "SaleDeliveryMailOutbox" WHERE "RecipientEmail" IS NULL) OR
                        EXISTS (SELECT 1 FROM "ReservationDepositMailOutbox" WHERE "RecipientEmail" IS NULL) THEN
                        RAISE EXCEPTION 'Cannot revert AccountErasure while erased account data exists.';
                    END IF;
                END $$;
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER account_events_minimization ON "AccountEvents";
                DROP TRIGGER account_events_no_delete ON "AccountEvents";
                CREATE TRIGGER account_events_append_only BEFORE UPDATE OR DELETE ON "AccountEvents"
                    FOR EACH ROW EXECUTE FUNCTION forbid_account_event_change();
                DROP FUNCTION public.guard_account_event_minimization();

                DROP TRIGGER reservation_update_guard ON "Reservations";
                DROP TRIGGER reservation_guard ON "Reservations";
                CREATE TRIGGER reservation_guard BEFORE INSERT OR UPDATE OR DELETE ON "Reservations"
                    FOR EACH ROW EXECUTE FUNCTION guard_reservation();
                DROP FUNCTION public.guard_reservation_update();

                DROP TRIGGER protect_password_reset ON public."PasswordResetChallenges";
                CREATE TRIGGER protect_password_reset BEFORE UPDATE ON public."PasswordResetChallenges"
                    FOR EACH ROW EXECUTE FUNCTION public.protect_password_reset();
                DROP FUNCTION public.protect_password_reset_minimizing();

                DROP TRIGGER protect_appointment_mail ON public."AppointmentMailOutbox";
                CREATE TRIGGER protect_appointment_mail BEFORE UPDATE OR DELETE ON public."AppointmentMailOutbox"
                    FOR EACH ROW EXECUTE FUNCTION public.protect_appointment_mail();
                DROP FUNCTION public.protect_appointment_mail_minimizing();

                DROP TRIGGER test_drive_mail_guard ON "TestDriveMailOutbox";
                CREATE TRIGGER test_drive_mail_guard BEFORE INSERT OR UPDATE OR DELETE ON "TestDriveMailOutbox"
                    FOR EACH ROW EXECUTE FUNCTION guard_test_drive_mail();
                DROP FUNCTION public.guard_test_drive_mail_minimizing();

                DROP TRIGGER sale_delivery_mail_guard ON "SaleDeliveryMailOutbox";
                CREATE TRIGGER sale_delivery_mail_guard BEFORE INSERT OR UPDATE OR DELETE ON "SaleDeliveryMailOutbox"
                    FOR EACH ROW EXECUTE FUNCTION guard_sale_delivery_mail();
                DROP FUNCTION public.guard_sale_delivery_mail_minimizing();
                """);

            migrationBuilder.DropCheckConstraint(
                name: "CK_TestDriveMailOutbox_Shape",
                table: "TestDriveMailOutbox");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SaleDeliveryMailOutbox_Shape",
                table: "SaleDeliveryMailOutbox");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Reservations_Buyer",
                table: "Reservations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ReservationDepositMailOutbox_Shape",
                table: "ReservationDepositMailOutbox");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PasswordResetChallenges_Identity",
                table: "PasswordResetChallenges");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AppointmentMailOutbox_Shape",
                table: "AppointmentMailOutbox");

            migrationBuilder.DropIndex(
                name: "IX_AccountEvents_TargetId",
                table: "AccountEvents");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AccountEvents_Shape",
                table: "AccountEvents");

            migrationBuilder.DropColumn(
                name: "MinimizedAt",
                table: "AccountEvents");

            migrationBuilder.AlterColumn<string>(
                name: "RecipientEmail",
                table: "TestDriveMailOutbox",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "RecipientEmail",
                table: "SaleDeliveryMailOutbox",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "BuyerNric",
                table: "Reservations",
                type: "character varying(12)",
                maxLength: 12,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(12)",
                oldMaxLength: 12,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "BuyerMobile",
                table: "Reservations",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "BuyerEmail",
                table: "Reservations",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "RecipientEmail",
                table: "ReservationDepositMailOutbox",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "RecipientEmail",
                table: "PasswordResetChallenges",
                type: "character varying(254)",
                maxLength: 254,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(254)",
                oldMaxLength: 254,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "RecipientEmail",
                table: "EmailVerificationChallenges",
                type: "character varying(254)",
                maxLength: 254,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(254)",
                oldMaxLength: 254,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "RecipientEmail",
                table: "AppointmentMailOutbox",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_TestDriveMailOutbox_Shape",
                table: "TestDriveMailOutbox",
                sql: "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"RecipientEmail\" ~ '[^[:space:]]' AND \"DeliveryAttempts\" >= 0 AND isfinite(\"NextDeliveryAt\") AND ((\"DeliveryClaimId\" IS NULL) = (\"DeliveryClaimExpiresAt\" IS NULL))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SaleDeliveryMailOutbox_Shape",
                table: "SaleDeliveryMailOutbox",
                sql: "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"DeliveryAttempts\" >= 0 AND \"RecipientEmail\" ~ '[^[:space:]]' AND isfinite(\"NextDeliveryAt\") AND ((\"DeliveryClaimId\" IS NULL) = (\"DeliveryClaimExpiresAt\" IS NULL))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Reservations_Buyer",
                table: "Reservations",
                sql: "\"BuyerName\" ~ '[^[:space:]]' AND \"BuyerEmail\" ~ '[^[:space:]]' AND \"BuyerMobile\" ~ '[^[:space:]]' AND \"BuyerNric\" ~ '^[0-9]{12}$'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ReservationDepositMailOutbox_Shape",
                table: "ReservationDepositMailOutbox",
                sql: "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"DeliveryAttempts\" >= 0 AND \"RecipientEmail\" ~ '[^[:space:]]'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PasswordResetChallenges_Identity",
                table: "PasswordResetChallenges",
                sql: "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"RecipientEmail\" ~ '[^[:space:]]'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AppointmentMailOutbox_Shape",
                table: "AppointmentMailOutbox",
                sql: "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"RecipientEmail\" ~ '[^[:space:]]' AND \"DeliveryAttempts\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AccountEvents_Shape",
                table: "AccountEvents",
                sql: "isfinite(\"RecordedAt\") AND \"Kind\" IN ('UsernameChanged', 'PictureRemoved', 'ProfileEdited', 'EmailChanged', 'BranchTransferred', 'Suspended', 'SuspensionLifted', 'Deactivated', 'Reactivated', 'NricRevealed', 'SessionsRevoked', 'VerificationResent', 'ResetSent') AND ((\"Kind\" IN ('Suspended', 'Deactivated', 'BranchTransferred', 'NricRevealed', 'SessionsRevoked', 'VerificationResent', 'ResetSent', 'ProfileEdited', 'EmailChanged') AND \"Reason\" IS NOT NULL AND \"Reason\" = btrim(\"Reason\") AND length(\"Reason\") BETWEEN 1 AND 1000 AND \"Reason\" !~ '[[:cntrl:]]') OR (\"Kind\" NOT IN ('Suspended', 'Deactivated', 'BranchTransferred', 'NricRevealed', 'SessionsRevoked', 'VerificationResent', 'ResetSent', 'ProfileEdited', 'EmailChanged') AND \"Reason\" IS NULL)) AND (\"Changes\" IS NULL OR jsonb_typeof(\"Changes\") = 'array')");
        }
    }
}

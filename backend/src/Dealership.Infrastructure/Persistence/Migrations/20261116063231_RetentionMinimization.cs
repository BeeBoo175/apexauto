using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dealership.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RetentionMinimization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_StaffInvitations_Profile",
                table: "StaffInvitations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PasswordResetChallenges_Receipt",
                table: "PasswordResetChallenges");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AccountSessions_Stamp",
                table: "AccountSessions");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "MinimizedAt",
                table: "StaffInvitations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "MinimizedAt",
                table: "PasswordResetChallenges",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "SecurityStamp",
                table: "AccountSessions",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "MinimizedAt",
                table: "AccountSessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RetentionSettingChanges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Setting = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PreviousValue = table.Column<int>(type: "integer", nullable: false),
                    NewValue = table.Column<int>(type: "integer", nullable: false),
                    SettingsVersion = table.Column<long>(type: "bigint", nullable: false),
                    ShorteningConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChangedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RetentionSettingChanges", x => x.Id);
                    table.CheckConstraint("CK_RetentionSettingChanges_Shape", "\"Setting\" IN ('SecurityArtifactDays', 'MailDetailDays', 'InvitationDays', 'BusinessEvidenceYears') AND \"PreviousValue\" > 0 AND \"NewValue\" > 0 AND \"PreviousValue\" <> \"NewValue\" AND \"SettingsVersion\" > 1 AND isfinite(\"ChangedAtUtc\") AND (NOT \"ShorteningConfirmed\" OR \"NewValue\" < \"PreviousValue\")");
                    table.ForeignKey(
                        name: "FK_RetentionSettingChanges_AspNetUsers_ActorId",
                        column: x => x.ActorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RetentionSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    SecurityArtifactDays = table.Column<int>(type: "integer", nullable: false),
                    MailDetailDays = table.Column<int>(type: "integer", nullable: false),
                    InvitationDays = table.Column<int>(type: "integer", nullable: false),
                    BusinessEvidenceYears = table.Column<int>(type: "integer", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RetentionSettings", x => x.Id);
                    table.CheckConstraint("CK_RetentionSettings_Shape", "\"Id\" = 1 AND \"SecurityArtifactDays\" BETWEEN 7 AND 365 AND \"MailDetailDays\" BETWEEN 7 AND 365 AND \"InvitationDays\" BETWEEN 7 AND 365 AND \"BusinessEvidenceYears\" BETWEEN 1 AND 10 AND \"Version\" > 0 AND isfinite(\"UpdatedAtUtc\") AND \"UpdatedById\" <> '00000000-0000-0000-0000-000000000000'::uuid");
                    table.ForeignKey(
                        name: "FK_RetentionSettings_AspNetUsers_UpdatedById",
                        column: x => x.UpdatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_StaffInvitations_Profile",
                table: "StaffInvitations",
                sql: "(\"MinimizedAt\" IS NULL AND \"Email\" IS NOT NULL AND \"Name\" IS NOT NULL AND \"Mobile\" IS NOT NULL AND \"Name\" ~ '[^[:space:]]' AND \"Mobile\" ~ '[^[:space:]]') OR ((\"ActivatedAt\" IS NOT NULL OR \"MinimizedAt\" IS NOT NULL) AND \"Email\" IS NULL AND \"Name\" IS NULL AND \"Mobile\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PasswordResetChallenges_Receipt",
                table: "PasswordResetChallenges",
                sql: "(\"ConsumedAt\" IS NULL AND \"CompletedSecurityStamp\" IS NULL) OR\r\n(\"ConsumedAt\" IS NOT NULL AND\r\n ((\"CompletedSecurityStamp\" IS NOT NULL AND length(\"CompletedSecurityStamp\") > 0) OR\r\n  (\"CompletedSecurityStamp\" IS NULL AND \"MinimizedAt\" IS NOT NULL)) AND\r\n \"ConsumedAt\" >= \"CreatedAt\" AND \"ConsumedAt\" < \"ExpiresAt\" AND \"InvalidatedAt\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AccountSessions_Stamp",
                table: "AccountSessions",
                sql: "(\"MinimizedAt\" IS NULL AND \"SecurityStamp\" IS NOT NULL AND \"SecurityStamp\" ~ '[^[:space:]]') OR (\"MinimizedAt\" IS NOT NULL AND \"SecurityStamp\" IS NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_RetentionSettingChanges_ActorId",
                table: "RetentionSettingChanges",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_RetentionSettingChanges_Setting_SettingsVersion",
                table: "RetentionSettingChanges",
                columns: new[] { "Setting", "SettingsVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RetentionSettings_UpdatedById",
                table: "RetentionSettings",
                column: "UpdatedById");

            // The change receipt is append-only for every statement type; a new table, so a new function.
            migrationBuilder.Sql("""
                CREATE FUNCTION public.forbid_retention_setting_change() RETURNS trigger
                LANGUAGE plpgsql SET search_path = pg_catalog AS $$
                BEGIN
                    RAISE EXCEPTION 'Retention setting changes are append-only'
                        USING ERRCODE = '23514', CONSTRAINT = 'CK_RetentionSettingChanges_Immutable';
                END $$;
                CREATE TRIGGER retention_setting_changes_append_only BEFORE UPDATE OR DELETE ON "RetentionSettingChanges"
                    FOR EACH ROW EXECUTE FUNCTION public.forbid_retention_setting_change();
                CREATE TRIGGER retention_setting_changes_no_truncate BEFORE TRUNCATE ON "RetentionSettingChanges"
                    FOR EACH STATEMENT EXECUTE FUNCTION public.forbid_retention_setting_change();
                """);

            // Each routine minimization is a narrow branch of an existing per-table function: only the listed
            // columns may become NULL, only on a terminal row, and the marker is set once. The retention period
            // itself is enforced by the executor, never by a trigger. Down() restores the previous bodies.
            migrationBuilder.Sql("""
                CREATE FUNCTION public.protect_account_session_minimizing() RETURNS trigger
                LANGUAGE plpgsql SET search_path = pg_catalog AS $$
                BEGIN
                    IF (NEW."Id", NEW."UserId", NEW."CreatedAt", NEW."ExpiresAt")
                        IS DISTINCT FROM
                        (OLD."Id", OLD."UserId", OLD."CreatedAt", OLD."ExpiresAt")
                        OR (OLD."RevokedAt" IS NOT NULL AND NEW."RevokedAt" IS DISTINCT FROM OLD."RevokedAt")
                        OR (OLD."MinimizedAt" IS NOT NULL AND
                            (NEW."MinimizedAt", NEW."SecurityStamp") IS DISTINCT FROM (OLD."MinimizedAt", OLD."SecurityStamp"))
                        OR (NEW."SecurityStamp" IS DISTINCT FROM OLD."SecurityStamp" AND NOT
                            (NEW."SecurityStamp" IS NULL AND NEW."MinimizedAt" IS NOT NULL AND
                             (OLD."RevokedAt" IS NOT NULL OR OLD."ExpiresAt" <= now()))) THEN
                        RAISE EXCEPTION 'Session identity, lifetime and completed revocation are immutable.'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_AccountSessions_Immutable';
                    END IF;
                    RETURN NEW;
                END;
                $$;
                DROP TRIGGER protect_account_session ON public."AccountSessions";
                CREATE TRIGGER protect_account_session BEFORE UPDATE ON public."AccountSessions"
                    FOR EACH ROW EXECUTE FUNCTION public.protect_account_session_minimizing();
                """);

            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION public.protect_password_reset_minimizing() RETURNS trigger
                LANGUAGE plpgsql SET search_path = pg_catalog AS $$
                BEGIN
                    IF (NEW."Id", NEW."UserId", NEW."CreatedAt", NEW."ExpiresAt")
                        IS DISTINCT FROM
                        (OLD."Id", OLD."UserId", OLD."CreatedAt", OLD."ExpiresAt")
                        OR (NEW."RecipientEmail" IS DISTINCT FROM OLD."RecipientEmail" AND NOT
                            (NEW."RecipientEmail" IS NULL AND (
                                (OLD."MinimizedAt" IS NULL AND NEW."MinimizedAt" IS NOT NULL AND
                                    (OLD."ConsumedAt" IS NOT NULL OR OLD."InvalidatedAt" IS NOT NULL OR OLD."ExpiresAt" <= now())) OR
                                EXISTS (SELECT 1 FROM public."AspNetUsers" u
                                    WHERE u."Id" = OLD."UserId" AND u."ClosedAt" IS NOT NULL))))
                        OR (OLD."ConsumedAt" IS NOT NULL AND
                            (NEW."ConsumedAt", NEW."CompletedSecurityStamp") IS DISTINCT FROM
                            (OLD."ConsumedAt", OLD."CompletedSecurityStamp") AND NOT
                            (NEW."ConsumedAt" = OLD."ConsumedAt" AND NEW."CompletedSecurityStamp" IS NULL AND
                             OLD."MinimizedAt" IS NULL AND NEW."MinimizedAt" IS NOT NULL))
                        OR (OLD."InvalidatedAt" IS NOT NULL AND
                            NEW."InvalidatedAt" IS DISTINCT FROM OLD."InvalidatedAt")
                        OR (OLD."MinimizedAt" IS NOT NULL AND NEW."MinimizedAt" IS DISTINCT FROM OLD."MinimizedAt") THEN
                        RAISE EXCEPTION 'Reset identity, lifetime and completed receipts are immutable.'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_PasswordResetChallenges_Immutable';
                    END IF;
                    RETURN NEW;
                END;
                $$;
                """);

            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION public.protect_appointment_mail_minimizing() RETURNS trigger
                LANGUAGE plpgsql SET search_path = pg_catalog AS $$
                BEGIN
                    IF TG_OP = 'DELETE' OR
                        (NEW."Id", NEW."AppointmentId", NEW."Kind") IS DISTINCT FROM
                        (OLD."Id", OLD."AppointmentId", OLD."Kind") OR
                        (NEW."RecipientEmail" IS DISTINCT FROM OLD."RecipientEmail" AND NOT
                            (NEW."RecipientEmail" IS NULL AND (OLD."DeliveredAt" IS NOT NULL OR
                                EXISTS (SELECT 1 FROM public."IntakeAppointments" a
                                    JOIN public."AspNetUsers" u ON u."Id" = a."OwnerId"
                                    WHERE a."Id" = OLD."AppointmentId" AND u."ClosedAt" IS NOT NULL)))) OR
                        (OLD."DeliveredAt" IS NOT NULL AND NEW."DeliveredAt" IS DISTINCT FROM OLD."DeliveredAt") THEN
                        RAISE EXCEPTION 'Appointment mail identity, recipient and first delivery receipt are immutable.'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_AppointmentMailOutbox_Immutable';
                    END IF;
                    RETURN NEW;
                END;
                $$;
                """);

            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION public.guard_test_drive_mail_minimizing() RETURNS trigger
                LANGUAGE plpgsql SET search_path = pg_catalog AS $$
                BEGIN
                    IF TG_OP = 'DELETE' OR (TG_OP = 'UPDATE' AND (
                        ROW(NEW."Id", NEW."BookingId", NEW."Kind") IS DISTINCT FROM
                        ROW(OLD."Id", OLD."BookingId", OLD."Kind") OR
                        (NEW."RecipientEmail" IS DISTINCT FROM OLD."RecipientEmail" AND NOT
                            (NEW."RecipientEmail" IS NULL AND (OLD."DeliveredAt" IS NOT NULL OR
                                EXISTS (SELECT 1 FROM public."TestDriveBookings" b
                                    JOIN public."AspNetUsers" u ON u."Id" = b."CustomerId"
                                    WHERE b."Id" = OLD."BookingId" AND u."ClosedAt" IS NOT NULL)))) OR
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
                """);

            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION public.guard_sale_delivery_mail_minimizing() RETURNS trigger
                LANGUAGE plpgsql SET search_path = pg_catalog AS $$
                BEGIN
                    IF TG_OP = 'DELETE' OR (TG_OP = 'UPDATE' AND (
                        ROW(NEW."Id", NEW."SalesOrderId", NEW."DeliveryOperationId") IS DISTINCT FROM
                        ROW(OLD."Id", OLD."SalesOrderId", OLD."DeliveryOperationId") OR
                        (NEW."RecipientEmail" IS DISTINCT FROM OLD."RecipientEmail" AND NOT
                            (NEW."RecipientEmail" IS NULL AND (OLD."DeliveredAt" IS NOT NULL OR
                                EXISTS (SELECT 1 FROM public."Reservations" r
                                    JOIN public."AspNetUsers" u ON u."Id" = r."CustomerId"
                                    WHERE r."Id" = OLD."SalesOrderId" AND u."ClosedAt" IS NOT NULL)))))) THEN
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
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RetentionSettingChanges");

            // Restoring the earlier checks and NOT NULL stamp would need invented values, so refuse instead.
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "AccountSessions" WHERE "MinimizedAt" IS NOT NULL) OR
                        EXISTS (SELECT 1 FROM "PasswordResetChallenges" WHERE "MinimizedAt" IS NOT NULL) OR
                        EXISTS (SELECT 1 FROM "StaffInvitations" WHERE "MinimizedAt" IS NOT NULL) THEN
                        RAISE EXCEPTION 'Cannot revert RetentionMinimization while minimized rows exist.';
                    END IF;
                END $$;
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER protect_account_session ON public."AccountSessions";
                CREATE TRIGGER protect_account_session BEFORE UPDATE ON public."AccountSessions"
                    FOR EACH ROW EXECUTE FUNCTION public.protect_account_session();
                DROP FUNCTION public.protect_account_session_minimizing();

                CREATE OR REPLACE FUNCTION public.protect_password_reset_minimizing() RETURNS trigger
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

                CREATE OR REPLACE FUNCTION public.protect_appointment_mail_minimizing() RETURNS trigger
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

                CREATE OR REPLACE FUNCTION public.guard_test_drive_mail_minimizing() RETURNS trigger
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

                CREATE OR REPLACE FUNCTION public.guard_sale_delivery_mail_minimizing() RETURNS trigger
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
                """);

            migrationBuilder.DropTable(
                name: "RetentionSettings");

            migrationBuilder.Sql("DROP FUNCTION public.forbid_retention_setting_change();");

            migrationBuilder.DropCheckConstraint(
                name: "CK_StaffInvitations_Profile",
                table: "StaffInvitations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PasswordResetChallenges_Receipt",
                table: "PasswordResetChallenges");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AccountSessions_Stamp",
                table: "AccountSessions");

            migrationBuilder.DropColumn(
                name: "MinimizedAt",
                table: "StaffInvitations");

            migrationBuilder.DropColumn(
                name: "MinimizedAt",
                table: "PasswordResetChallenges");

            migrationBuilder.DropColumn(
                name: "MinimizedAt",
                table: "AccountSessions");

            migrationBuilder.AlterColumn<string>(
                name: "SecurityStamp",
                table: "AccountSessions",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_StaffInvitations_Profile",
                table: "StaffInvitations",
                sql: "(\"Email\" IS NOT NULL AND \"Name\" IS NOT NULL AND \"Mobile\" IS NOT NULL AND \"Name\" ~ '[^[:space:]]' AND \"Mobile\" ~ '[^[:space:]]') OR (\"ActivatedAt\" IS NOT NULL AND \"Email\" IS NULL AND \"Name\" IS NULL AND \"Mobile\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PasswordResetChallenges_Receipt",
                table: "PasswordResetChallenges",
                sql: "(\"ConsumedAt\" IS NULL AND \"CompletedSecurityStamp\" IS NULL) OR\r\n(\"ConsumedAt\" IS NOT NULL AND \"CompletedSecurityStamp\" IS NOT NULL AND\r\n length(\"CompletedSecurityStamp\") > 0 AND \"ConsumedAt\" >= \"CreatedAt\" AND\r\n \"ConsumedAt\" < \"ExpiresAt\" AND \"InvalidatedAt\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AccountSessions_Stamp",
                table: "AccountSessions",
                sql: "\"SecurityStamp\" ~ '[^[:space:]]'");
        }
    }
}

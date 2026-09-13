using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dealership.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RetentionAppointmentDisposal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // One deadline function serves every seven-year chain: the years in force are the larger of the current
            // setting and the value that applied at the anchor, read from the change receipt, so a shortening never
            // reaches earlier commitments and an extension always does. Calendar years in UTC.
            migrationBuilder.Sql("""
                CREATE FUNCTION public.retention_business_deadline(anchor timestamptz) RETURNS timestamptz
                LANGUAGE sql STABLE SET search_path = pg_catalog AS $$
                    SELECT ((anchor AT TIME ZONE 'UTC') + make_interval(years => GREATEST(
                        COALESCE((SELECT "BusinessEvidenceYears" FROM public."RetentionSettings" WHERE "Id" = 1), 7),
                        COALESCE(
                            (SELECT c."NewValue" FROM public."RetentionSettingChanges" c
                                WHERE c."Setting" = 'BusinessEvidenceYears' AND c."ChangedAtUtc" <= anchor
                                ORDER BY c."ChangedAtUtc" DESC LIMIT 1),
                            (SELECT c."PreviousValue" FROM public."RetentionSettingChanges" c
                                WHERE c."Setting" = 'BusinessEvidenceYears' AND c."ChangedAtUtc" > anchor
                                ORDER BY c."ChangedAtUtc" ASC LIMIT 1),
                            7)))) AT TIME ZONE 'UTC'
                $$;
                """);

            // Check-in and test-drive outcome notes are the only nullable staff free text of these chains. Each table
            // gets its own function; the shared immutability functions stay untouched.
            migrationBuilder.Sql("""
                CREATE FUNCTION public.guard_checkin_minimization() RETURNS trigger
                LANGUAGE plpgsql SET search_path = pg_catalog AS $$
                BEGIN
                    IF TG_OP = 'UPDATE' AND OLD."Notes" IS NOT NULL AND NEW."Notes" IS NULL AND
                        (to_jsonb(NEW) - 'Notes') = (to_jsonb(OLD) - 'Notes') THEN
                        RETURN NEW;
                    END IF;
                    RAISE EXCEPTION 'Appointment identity, valuation and photo evidence are immutable.'
                        USING ERRCODE = '23514', CONSTRAINT = 'CK_IntakeAppointments_Immutable';
                END;
                $$;
                DROP TRIGGER protect_checkin_evidence ON public."AppointmentCheckIns";
                CREATE TRIGGER protect_checkin_evidence BEFORE UPDATE OR DELETE ON public."AppointmentCheckIns"
                    FOR EACH ROW EXECUTE FUNCTION public.guard_checkin_minimization();

                CREATE FUNCTION public.guard_test_drive_outcome_minimization() RETURNS trigger
                LANGUAGE plpgsql SET search_path = pg_catalog AS $$
                BEGIN
                    IF TG_OP = 'UPDATE' AND OLD."Notes" IS NOT NULL AND NEW."Notes" IS NULL AND
                        (to_jsonb(NEW) - 'Notes') = (to_jsonb(OLD) - 'Notes') THEN
                        RETURN NEW;
                    END IF;
                    RAISE EXCEPTION 'Test-drive handling receipts are immutable'
                        USING ERRCODE = '23514', CONSTRAINT = 'CK_TestDriveHandling_Immutable';
                END $$;
                DROP TRIGGER test_drive_outcome_guard ON "TestDriveOutcomes";
                CREATE TRIGGER test_drive_outcome_guard BEFORE INSERT ON "TestDriveOutcomes"
                    FOR EACH ROW EXECUTE FUNCTION guard_test_drive_handling();
                CREATE TRIGGER test_drive_outcome_update_guard BEFORE UPDATE OR DELETE ON "TestDriveOutcomes"
                    FOR EACH ROW EXECUTE FUNCTION public.guard_test_drive_outcome_minimization();
                """);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER test_drive_outcome_update_guard ON "TestDriveOutcomes";
                DROP TRIGGER test_drive_outcome_guard ON "TestDriveOutcomes";
                CREATE TRIGGER test_drive_outcome_guard BEFORE INSERT OR UPDATE OR DELETE ON "TestDriveOutcomes"
                    FOR EACH ROW EXECUTE FUNCTION guard_test_drive_handling();
                DROP FUNCTION public.guard_test_drive_outcome_minimization();

                DROP TRIGGER protect_checkin_evidence ON public."AppointmentCheckIns";
                CREATE TRIGGER protect_checkin_evidence BEFORE UPDATE OR DELETE ON public."AppointmentCheckIns"
                    FOR EACH ROW EXECUTE FUNCTION public.protect_appointment_evidence();
                DROP FUNCTION public.guard_checkin_minimization();

                DROP FUNCTION public.retention_business_deadline(timestamptz);
                """);

        }
    }
}

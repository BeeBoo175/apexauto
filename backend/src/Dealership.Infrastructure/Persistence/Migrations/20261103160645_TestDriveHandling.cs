using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dealership.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TestDriveHandling : Migration
    {
        private static readonly string[] BookingVehicleBranchColumns = ["Id", "VehicleId", "BranchId"];
        private static readonly string[] BookingBranchColumns = ["Id", "BranchId"];
        private static readonly string[] HandlingBookingBranchColumns = ["Id", "BookingId", "BranchId"];
        private static readonly string[] StartVehicleScopeColumns = ["Id", "BookingId", "VehicleId", "BranchId"];
        private static readonly string[] AssignmentScopeColumns = ["AssignmentId", "BookingId", "BranchId"];
        private static readonly string[] BookingScopeColumns = ["BookingId", "BranchId"];
        private static readonly string[] BookingVehicleScopeColumns = ["BookingId", "VehicleId", "BranchId"];
        private static readonly string[] StartScopeColumns = ["StartId", "BookingId", "BranchId"];
        private static readonly string[] OccupationScopeColumns = ["StartId", "BookingId", "VehicleId", "BranchId"];
        private static readonly string[] OutcomeScopeColumns = ["OutcomeId", "BookingId", "BranchId"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_TestDriveBookings_Id_VehicleId_BranchId",
                table: "TestDriveBookings",
                columns: BookingVehicleBranchColumns);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_TestDriveAssignments_Id_BookingId_BranchId",
                table: "TestDriveAssignments",
                columns: HandlingBookingBranchColumns);

            migrationBuilder.CreateTable(
                name: "TestDriveStarts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BookingId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    StaffId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerPresent = table.Column<bool>(type: "boolean", nullable: false),
                    DrivingLicenceChecked = table.Column<bool>(type: "boolean", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestDriveStarts", x => x.Id);
                    table.UniqueConstraint("AK_TestDriveStarts_Id_BookingId_BranchId", x => new { x.Id, x.BookingId, x.BranchId });
                    table.UniqueConstraint("AK_TestDriveStarts_Id_BookingId_VehicleId_BranchId", x => new { x.Id, x.BookingId, x.VehicleId, x.BranchId });
                    table.CheckConstraint("CK_TestDriveStarts_Shape", "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"CustomerPresent\" AND \"DrivingLicenceChecked\" AND isfinite(\"StartedAt\")");
                    table.ForeignKey(
                        name: "FK_TestDriveStarts_AspNetUsers_StaffId",
                        column: x => x.StaffId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TestDriveStarts_TestDriveAssignments_AssignmentId_BookingId~",
                        columns: x => new { x.AssignmentId, x.BookingId, x.BranchId },
                        principalTable: "TestDriveAssignments",
                        principalColumns: HandlingBookingBranchColumns,
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TestDriveStarts_TestDriveBookings_BookingId_VehicleId_Branc~",
                        columns: x => new { x.BookingId, x.VehicleId, x.BranchId },
                        principalTable: "TestDriveBookings",
                        principalColumns: BookingVehicleBranchColumns,
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TestDriveOutcomes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BookingId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    StaffId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartId = table.Column<Guid>(type: "uuid", nullable: true),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CustomerPresent = table.Column<bool>(type: "boolean", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    EndedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestDriveOutcomes", x => x.Id);
                    table.UniqueConstraint("AK_TestDriveOutcomes_Id_BookingId_BranchId", x => new { x.Id, x.BookingId, x.BranchId });
                    table.CheckConstraint("CK_TestDriveOutcomes_Shape", "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"CustomerPresent\" AND isfinite(\"EndedAt\") AND ((\"Kind\" = 'Completed' AND \"StartId\" IS NOT NULL AND \"Reason\" IS NULL) OR (\"Kind\" = 'NotCompleted' AND \"Reason\" IS NOT NULL))");
                    table.CheckConstraint("CK_TestDriveOutcomes_Text", "(\"Reason\" IS NULL OR (length(\"Reason\") BETWEEN 1 AND 1000 AND \"Reason\" = btrim(\"Reason\") AND \"Reason\" ~ '[^[:space:]]' AND \"Reason\" !~ '[[:cntrl:]]')) AND (\"Notes\" IS NULL OR (length(\"Notes\") BETWEEN 1 AND 1000 AND \"Notes\" = btrim(\"Notes\") AND \"Notes\" ~ '[^[:space:]]' AND \"Notes\" !~ '[[:cntrl:]]'))");
                    table.ForeignKey(
                        name: "FK_TestDriveOutcomes_AspNetUsers_StaffId",
                        column: x => x.StaffId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TestDriveOutcomes_TestDriveAssignments_AssignmentId_Booking~",
                        columns: x => new { x.AssignmentId, x.BookingId, x.BranchId },
                        principalTable: "TestDriveAssignments",
                        principalColumns: HandlingBookingBranchColumns,
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TestDriveOutcomes_TestDriveBookings_BookingId_BranchId",
                        columns: x => new { x.BookingId, x.BranchId },
                        principalTable: "TestDriveBookings",
                        principalColumns: BookingBranchColumns,
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TestDriveOutcomes_TestDriveStarts_StartId_BookingId_BranchId",
                        columns: x => new { x.StartId, x.BookingId, x.BranchId },
                        principalTable: "TestDriveStarts",
                        principalColumns: HandlingBookingBranchColumns,
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TestDriveOccupations",
                columns: table => new
                {
                    StartId = table.Column<Guid>(type: "uuid", nullable: false),
                    BookingId = table.Column<Guid>(type: "uuid", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    OutcomeId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestDriveOccupations", x => x.StartId);
                    table.ForeignKey(
                        name: "FK_TestDriveOccupations_TestDriveOutcomes_OutcomeId_BookingId_~",
                        columns: x => new { x.OutcomeId, x.BookingId, x.BranchId },
                        principalTable: "TestDriveOutcomes",
                        principalColumns: HandlingBookingBranchColumns,
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TestDriveOccupations_TestDriveStarts_StartId_BookingId_Vehi~",
                        columns: x => new { x.StartId, x.BookingId, x.VehicleId, x.BranchId },
                        principalTable: "TestDriveStarts",
                        principalColumns: StartVehicleScopeColumns,
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TestDriveOccupations_OutcomeId_BookingId_BranchId",
                table: "TestDriveOccupations",
                columns: OutcomeScopeColumns);

            migrationBuilder.CreateIndex(
                name: "IX_TestDriveOccupations_StartId_BookingId_VehicleId_BranchId",
                table: "TestDriveOccupations",
                columns: OccupationScopeColumns,
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TestDriveOccupations_VehicleId",
                table: "TestDriveOccupations",
                column: "VehicleId",
                unique: true,
                filter: "\"OutcomeId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TestDriveOutcomes_AssignmentId_BookingId_BranchId",
                table: "TestDriveOutcomes",
                columns: AssignmentScopeColumns);

            migrationBuilder.CreateIndex(
                name: "IX_TestDriveOutcomes_BookingId",
                table: "TestDriveOutcomes",
                column: "BookingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TestDriveOutcomes_BookingId_BranchId",
                table: "TestDriveOutcomes",
                columns: BookingScopeColumns);

            migrationBuilder.CreateIndex(
                name: "IX_TestDriveOutcomes_StaffId",
                table: "TestDriveOutcomes",
                column: "StaffId");

            migrationBuilder.CreateIndex(
                name: "IX_TestDriveOutcomes_StartId_BookingId_BranchId",
                table: "TestDriveOutcomes",
                columns: StartScopeColumns);

            migrationBuilder.CreateIndex(
                name: "IX_TestDriveStarts_AssignmentId_BookingId_BranchId",
                table: "TestDriveStarts",
                columns: AssignmentScopeColumns);

            migrationBuilder.CreateIndex(
                name: "IX_TestDriveStarts_BookingId",
                table: "TestDriveStarts",
                column: "BookingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TestDriveStarts_BookingId_VehicleId_BranchId",
                table: "TestDriveStarts",
                columns: BookingVehicleScopeColumns);

            migrationBuilder.CreateIndex(
                name: "IX_TestDriveStarts_StaffId",
                table: "TestDriveStarts",
                column: "StaffId");
            CreateHandlingGuards(migrationBuilder);
            UpdateExistingGuards(migrationBuilder, restoring: false);
            migrationBuilder.Sql("""
                DO $$ DECLARE permission record; target text;
                BEGIN
                    FOR permission IN SELECT grantee, privilege_type FROM information_schema.table_privileges
                        WHERE table_schema = 'public' AND table_name = 'TestDriveBookings' AND grantee <> current_user AND
                            privilege_type IN ('SELECT', 'INSERT', 'UPDATE') LOOP
                        FOREACH target IN ARRAY ARRAY['TestDriveStarts', 'TestDriveOutcomes', 'TestDriveOccupations'] LOOP
                            EXECUTE format('GRANT %s ON TABLE %I TO %s', permission.privilege_type, target,
                                CASE WHEN permission.grantee = 'PUBLIC' THEN 'PUBLIC' ELSE quote_ident(permission.grantee) END);
                        END LOOP;
                    END LOOP;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "TestDriveStarts") OR EXISTS (SELECT 1 FROM "TestDriveOutcomes") THEN
                        RAISE EXCEPTION 'Retained test-drive handling evidence prevents rollback';
                    END IF;
                END $$;
                """);
            UpdateExistingGuards(migrationBuilder, restoring: true);
            migrationBuilder.Sql("""
                DROP TRIGGER test_drive_terminal_cancellation_guard ON "TestDriveCancellations";
                DROP TRIGGER test_drive_start_occupation ON "TestDriveStarts";
                DROP TRIGGER test_drive_outcome_occupation ON "TestDriveOutcomes";
                DROP TRIGGER test_drive_start_guard ON "TestDriveStarts";
                DROP TRIGGER test_drive_outcome_guard ON "TestDriveOutcomes";
                DROP TRIGGER test_drive_occupation_guard ON "TestDriveOccupations";
                DROP TRIGGER test_drive_start_bundle ON "TestDriveStarts";
                DROP TRIGGER test_drive_outcome_bundle ON "TestDriveOutcomes";
                DROP FUNCTION guard_test_drive_terminal_cancellation();
                DROP FUNCTION apply_test_drive_occupation();
                DROP FUNCTION guard_test_drive_handling();
                DROP FUNCTION guard_test_drive_occupation();
                DROP FUNCTION require_test_drive_occupation();
                DROP TRIGGER zz_test_drive_occupation_guard ON "Reservations";
                DROP TRIGGER zz_test_drive_occupation_guard ON "SalesOrders";
                DROP TRIGGER zz_test_drive_occupation_guard ON "InspectionDrafts";
                DROP TRIGGER zz_test_drive_occupation_guard ON "Vehicles";
                DROP FUNCTION guard_occupied_test_drive_vehicle();
                """);
            migrationBuilder.DropTable(
                name: "TestDriveOccupations");

            migrationBuilder.DropTable(
                name: "TestDriveOutcomes");

            migrationBuilder.DropTable(
                name: "TestDriveStarts");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_TestDriveBookings_Id_VehicleId_BranchId",
                table: "TestDriveBookings");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_TestDriveAssignments_Id_BookingId_BranchId",
                table: "TestDriveAssignments");
        }

        private static void CreateHandlingGuards(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE FUNCTION guard_test_drive_handling() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE
                    booking "TestDriveBookings"%ROWTYPE;
                    assignment "TestDriveAssignments"%ROWTYPE;
                    actor "AspNetUsers"%ROWTYPE;
                    started "TestDriveStarts"%ROWTYPE;
                    recorded_at timestamptz;
                    day_start timestamptz;
                    day_end timestamptz;
                BEGIN
                    IF TG_OP <> 'INSERT' THEN
                        RAISE EXCEPTION 'Test-drive handling receipts are immutable'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_TestDriveHandling_Immutable';
                    END IF;
                    PERFORM pg_advisory_xact_lock(hashtextextended('test-drive-handling:' || NEW."Id"::text, 0));
                    SELECT * INTO actor FROM "AspNetUsers" WHERE "Id" = NEW."StaffId" FOR SHARE;
                    UPDATE "Branches" SET "IsActive" = "IsActive" WHERE "Id" = NEW."BranchId" AND "IsActive";
                    IF NOT FOUND OR actor."Id" IS NULL OR NOT actor."IsActive" OR
                        actor."BranchId" IS DISTINCT FROM NEW."BranchId" OR
                        actor."Role" NOT IN ('SalesAgent', 'BranchManager') THEN
                        RAISE EXCEPTION 'Handling requires current eligible branch staff'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_TestDriveHandling_Eligibility';
                    END IF;
                    SELECT * INTO booking FROM "TestDriveBookings" WHERE "Id" = NEW."BookingId";
                    -- The vehicle write fences stale snapshots without changing inspection or purchase state.
                    UPDATE "Vehicles" SET "Status" = "Status" WHERE "Id" = booking."VehicleId";
                    SELECT * INTO assignment FROM "TestDriveAssignments" WHERE "BookingId" = booking."Id"
                        ORDER BY "Version" DESC LIMIT 1;
                    IF booking."Id" IS NULL OR booking."BranchId" <> NEW."BranchId" OR
                        assignment."Id" IS DISTINCT FROM NEW."AssignmentId" OR
                        assignment."HandlerId" IS DISTINCT FROM NEW."StaffId" OR
                        EXISTS (SELECT 1 FROM "TestDriveCancellations" WHERE "BookingId" = booking."Id") OR
                        EXISTS (SELECT 1 FROM "TestDriveOutcomes" WHERE "BookingId" = booking."Id") OR
                        EXISTS (SELECT 1 FROM "TestDriveStarts" WHERE "Id" = NEW."Id") OR
                        EXISTS (SELECT 1 FROM "TestDriveOutcomes" WHERE "Id" = NEW."Id") THEN
                        RAISE EXCEPTION 'Only the current handler records unfinished test drives'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_TestDriveHandling_Ownership';
                    END IF;
                    recorded_at := CASE WHEN TG_TABLE_NAME = 'TestDriveStarts'
                        THEN (to_jsonb(NEW)->>'StartedAt')::timestamptz ELSE (to_jsonb(NEW)->>'EndedAt')::timestamptz END;
                    day_start := (booking."StartsAt" AT TIME ZONE 'Asia/Kuala_Lumpur')::date::timestamp
                        AT TIME ZONE 'Asia/Kuala_Lumpur';
                    day_end := day_start + interval '1 day';
                    SELECT * INTO started FROM "TestDriveStarts" WHERE "BookingId" = booking."Id";
                    IF recorded_at < booking."CreatedAt" OR recorded_at < assignment."RecordedAt" OR
                        recorded_at > clock_timestamp() OR NOT isfinite(recorded_at) OR
                        (started."Id" IS NULL AND (recorded_at < day_start OR recorded_at >= day_end OR
                            clock_timestamp() < day_start OR clock_timestamp() >= day_end)) THEN
                        RAISE EXCEPTION 'Handling requires current chronological attendance evidence'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_TestDriveHandling_Chronology';
                    END IF;
                    IF TG_TABLE_NAME = 'TestDriveOutcomes' THEN
                        IF NEW."StartId" IS DISTINCT FROM started."Id" OR
                            (started."Id" IS NOT NULL AND recorded_at < started."StartedAt") THEN
                            RAISE EXCEPTION 'Outcome must retain the actual start'
                                USING ERRCODE = '23514', CONSTRAINT = 'CK_TestDriveOutcomes_Start';
                        END IF;
                        RETURN NEW;
                    END IF;
                    IF started."Id" IS NOT NULL OR NEW."VehicleId" <> booking."VehicleId" OR
                        EXISTS (SELECT 1 FROM "TestDriveOccupations" WHERE "VehicleId" = booking."VehicleId" AND "OutcomeId" IS NULL) OR
                        NOT EXISTS (SELECT 1 FROM "Vehicles" WHERE "Id" = booking."VehicleId" AND "Status" = 'Available') OR
                        EXISTS (SELECT 1 FROM "Reservations" WHERE "VehicleId" = booking."VehicleId" AND "IsActive") OR
                        EXISTS (SELECT 1 FROM "SalesOrders" WHERE "VehicleId" = booking."VehicleId" AND
                            "Status" IN ('Open', 'Handling', 'PendingApproval', 'Approved', 'Settled', 'Delivered')) OR
                        NOT EXISTS (SELECT 1 FROM "ListingPublications" p
                            JOIN "ListingRevisions" r ON r."Id" = p."RevisionId"
                            JOIN "InspectionSignedRevisions" s ON s."Id" = p."SignedRevisionId"
                            JOIN "InspectionDrafts" d ON d."Id" = s."InspectionId"
                            WHERE p."VehicleId" = booking."VehicleId" AND d."IsSigned" AND d."Revision" = s."Revision" AND
                                NOT EXISTS (SELECT 1 FROM "ListingWithdrawals" w WHERE w."PublicationId" = p."Id") AND
                                NOT EXISTS (SELECT 1 FROM "ListingPublications" later JOIN "ListingRevisions" lr ON lr."Id" = later."RevisionId"
                                    WHERE later."VehicleId" = p."VehicleId" AND lr."Version" > r."Version")) THEN
                        RAISE EXCEPTION 'Start requires an eligible unoccupied published vehicle'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_TestDriveStarts_Availability';
                    END IF;
                    IF public.staff_schedule_overlaps(NEW."StaffId", recorded_at, recorded_at + interval '30 minutes', NULL, booking."Id") OR
                        EXISTS (SELECT 1 FROM "TestDriveBookings" b WHERE b."Id" <> booking."Id" AND
                            b."VehicleId" = booking."VehicleId" AND b."StartsAt" < recorded_at + interval '30 minutes' AND
                            recorded_at < b."EndsAt" AND
                            NOT EXISTS (SELECT 1 FROM "TestDriveCancellations" c WHERE c."BookingId" = b."Id") AND
                            NOT EXISTS (SELECT 1 FROM "TestDriveOutcomes" o WHERE o."BookingId" = b."Id")) THEN
                        RAISE EXCEPTION 'Vehicle or handler has conflicting booked or active work'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_TestDriveStarts_Overlap';
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER test_drive_start_guard BEFORE INSERT OR UPDATE OR DELETE ON "TestDriveStarts"
                    FOR EACH ROW EXECUTE FUNCTION guard_test_drive_handling();
                CREATE TRIGGER test_drive_outcome_guard BEFORE INSERT OR UPDATE OR DELETE ON "TestDriveOutcomes"
                    FOR EACH ROW EXECUTE FUNCTION guard_test_drive_handling();

                CREATE FUNCTION guard_test_drive_occupation() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        RAISE EXCEPTION 'Vehicle occupation history is retained'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_TestDriveOccupations_Immutable';
                    END IF;
                    IF TG_OP = 'INSERT' THEN
                        IF NEW."OutcomeId" IS NOT NULL OR NOT EXISTS (SELECT 1 FROM "TestDriveStarts" s
                            WHERE s."Id" = NEW."StartId" AND s."BookingId" = NEW."BookingId" AND
                                s."VehicleId" = NEW."VehicleId" AND s."BranchId" = NEW."BranchId") THEN
                            RAISE EXCEPTION 'Occupation requires its actual start'
                                USING ERRCODE = '23514', CONSTRAINT = 'CK_TestDriveOccupations_Start';
                        END IF;
                        RETURN NEW;
                    END IF;
                    IF ROW(NEW."StartId", NEW."BookingId", NEW."VehicleId", NEW."BranchId") IS DISTINCT FROM
                        ROW(OLD."StartId", OLD."BookingId", OLD."VehicleId", OLD."BranchId") OR
                        OLD."OutcomeId" IS NOT NULL OR NEW."OutcomeId" IS NULL OR
                        NOT EXISTS (SELECT 1 FROM "TestDriveOutcomes" o WHERE o."Id" = NEW."OutcomeId" AND
                            o."StartId" = NEW."StartId" AND o."BookingId" = NEW."BookingId" AND o."BranchId" = NEW."BranchId") THEN
                        RAISE EXCEPTION 'Only the matching deliberate outcome releases occupation once'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_TestDriveOccupations_Immutable';
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER test_drive_occupation_guard BEFORE INSERT OR UPDATE OR DELETE ON "TestDriveOccupations"
                    FOR EACH ROW EXECUTE FUNCTION guard_test_drive_occupation();

                CREATE FUNCTION apply_test_drive_occupation() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_TABLE_NAME = 'TestDriveStarts' THEN
                        INSERT INTO "TestDriveOccupations" ("StartId", "BookingId", "VehicleId", "BranchId")
                            VALUES (NEW."Id", NEW."BookingId", NEW."VehicleId", NEW."BranchId");
                    ELSIF NEW."StartId" IS NOT NULL THEN
                        UPDATE "TestDriveOccupations" SET "OutcomeId" = NEW."Id" WHERE "StartId" = NEW."StartId";
                        IF NOT FOUND THEN
                            RAISE EXCEPTION 'Active start has no occupation'
                                USING ERRCODE = '23514', CONSTRAINT = 'CK_TestDriveOccupations_Bundle';
                        END IF;
                    END IF;
                    RETURN NULL;
                END $$;
                CREATE TRIGGER test_drive_start_occupation AFTER INSERT ON "TestDriveStarts"
                    FOR EACH ROW EXECUTE FUNCTION apply_test_drive_occupation();
                CREATE TRIGGER test_drive_outcome_occupation AFTER INSERT ON "TestDriveOutcomes"
                    FOR EACH ROW EXECUTE FUNCTION apply_test_drive_occupation();

                CREATE FUNCTION require_test_drive_occupation() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE start_id uuid;
                BEGIN
                    start_id := CASE WHEN TG_TABLE_NAME = 'TestDriveStarts' THEN NEW."Id"
                        ELSE (to_jsonb(NEW)->>'StartId')::uuid END;
                    IF start_id IS NOT NULL AND NOT EXISTS (SELECT 1 FROM "TestDriveOccupations" occupation
                        WHERE occupation."StartId" = start_id AND occupation."OutcomeId" IS NOT DISTINCT FROM
                            (SELECT o."Id" FROM "TestDriveOutcomes" o WHERE o."StartId" = start_id)) THEN
                        RAISE EXCEPTION 'Start, outcome and occupation must commit together'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_TestDriveOccupations_Bundle';
                    END IF;
                    RETURN NULL;
                END $$;
                CREATE CONSTRAINT TRIGGER test_drive_start_bundle AFTER INSERT ON "TestDriveStarts"
                    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION require_test_drive_occupation();
                CREATE CONSTRAINT TRIGGER test_drive_outcome_bundle AFTER INSERT ON "TestDriveOutcomes"
                    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION require_test_drive_occupation();

                CREATE FUNCTION guard_test_drive_terminal_cancellation() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "TestDriveStarts" WHERE "BookingId" = NEW."BookingId") OR
                        EXISTS (SELECT 1 FROM "TestDriveOutcomes" WHERE "BookingId" = NEW."BookingId") THEN
                        RAISE EXCEPTION 'Attended or started drives cannot be cancelled'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_TestDriveCancellations_Handling';
                    END IF;
                    RETURN NULL;
                END $$;
                CREATE TRIGGER test_drive_terminal_cancellation_guard AFTER INSERT ON "TestDriveCancellations"
                    FOR EACH ROW EXECUTE FUNCTION guard_test_drive_terminal_cancellation();

                CREATE FUNCTION guard_occupied_test_drive_vehicle() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE vehicle_id uuid;
                BEGIN
                    IF TG_TABLE_NAME = 'Vehicles' THEN
                        IF NEW."Status" = OLD."Status" THEN
                            RETURN NULL;
                        END IF;
                        vehicle_id := NEW."Id";
                    ELSE
                        vehicle_id := NEW."VehicleId";
                    END IF;
                    -- Existing writer admission has acquired its staff/branch locks; never acquire a later branch here.
                    PERFORM 1 FROM "Vehicles" WHERE "Id" = vehicle_id FOR UPDATE;
                    IF EXISTS (SELECT 1 FROM "TestDriveOccupations" WHERE "VehicleId" = vehicle_id AND "OutcomeId" IS NULL) THEN
                        RAISE EXCEPTION 'An active test drive exclusively occupies this vehicle'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_TestDriveOccupations_Exclusive';
                    END IF;
                    RETURN NULL;
                END $$;
                CREATE TRIGGER zz_test_drive_occupation_guard AFTER INSERT ON "Reservations"
                    FOR EACH ROW EXECUTE FUNCTION guard_occupied_test_drive_vehicle();
                CREATE TRIGGER zz_test_drive_occupation_guard AFTER INSERT OR UPDATE ON "SalesOrders"
                    FOR EACH ROW EXECUTE FUNCTION guard_occupied_test_drive_vehicle();
                CREATE TRIGGER zz_test_drive_occupation_guard AFTER INSERT OR UPDATE ON "InspectionDrafts"
                    FOR EACH ROW EXECUTE FUNCTION guard_occupied_test_drive_vehicle();
                CREATE TRIGGER zz_test_drive_occupation_guard AFTER UPDATE OF "Status" ON "Vehicles"
                    FOR EACH ROW EXECUTE FUNCTION guard_occupied_test_drive_vehicle();
                """);
        }

        private static void UpdateExistingGuards(MigrationBuilder migrationBuilder, bool restoring)
        {
            const string customer = "WHERE \"CustomerId\" = NEW.\"CustomerId\" AND \"StartsAt\" >= today AND NOT EXISTS (SELECT 1 FROM \"TestDriveCancellations\" c WHERE c.\"BookingId\" = \"TestDriveBookings\".\"Id\")";
            const string unfinished = " AND NOT EXISTS (SELECT 1 FROM \"TestDriveOutcomes\" o WHERE o.\"BookingId\" = \"TestDriveBookings\".\"Id\")";
            var activeCustomer = customer.Replace("\"StartsAt\" >= today",
                "(\"StartsAt\" >= today OR EXISTS (SELECT 1 FROM \"TestDriveOccupations\" occupation WHERE occupation.\"BookingId\" = \"TestDriveBookings\".\"Id\" AND occupation.\"OutcomeId\" IS NULL))") + unfinished;
            ReplaceFunction(migrationBuilder, "guard_test_drive_booking()", customer, activeCustomer, restoring);
            const string slot = "\"StartsAt\" < NEW.\"EndsAt\" AND \"EndsAt\" > NEW.\"StartsAt\" AND NOT EXISTS (SELECT 1 FROM \"TestDriveCancellations\" c WHERE c.\"BookingId\" = \"TestDriveBookings\".\"Id\")";
            ReplaceFunction(migrationBuilder, "guard_test_drive_booking()", slot, slot + unfinished, restoring);

            const string selector = "NOT EXISTS (SELECT 1 FROM \"TestDriveCancellations\" c WHERE c.\"BookingId\" = b.\"Id\")";
            const string unstarted = " AND NOT EXISTS (SELECT 1 FROM \"TestDriveStarts\" s WHERE s.\"BookingId\" = b.\"Id\") AND NOT EXISTS (SELECT 1 FROM \"TestDriveOutcomes\" o WHERE o.\"BookingId\" = b.\"Id\")";
            foreach (var function in new[] { "cancel_reserved_vehicle_test_drives()", "cancel_withdrawn_listing_test_drives()",
                "cancel_reopened_inspection_test_drives()", "cancel_inspection_repair_test_drives()" })
            {
                ReplaceFunction(migrationBuilder, function, selector, selector + unstarted, restoring);
            }

            const string overlap = "b.\"StartsAt\" < ends_at AND starts_at < b.\"EndsAt\"";
            const string activeOverlap = "(b.\"StartsAt\" < ends_at AND starts_at < b.\"EndsAt\" OR EXISTS (SELECT 1 FROM public.\"TestDriveOccupations\" occupation WHERE occupation.\"BookingId\" = b.\"Id\" AND occupation.\"OutcomeId\" IS NULL)) AND NOT EXISTS (SELECT 1 FROM public.\"TestDriveOutcomes\" o WHERE o.\"BookingId\" = b.\"Id\")";
            ReplaceFunction(migrationBuilder, "public.staff_schedule_overlaps(uuid,timestamptz,timestamptz,uuid,uuid)", overlap, activeOverlap, restoring);

            const string cancelled = "EXISTS (SELECT 1 FROM public.\"TestDriveCancellations\" WHERE \"BookingId\" = booking.\"Id\")";
            ReplaceFunction(migrationBuilder, "public.guard_test_drive_assignment()", cancelled,
                cancelled + " OR EXISTS (SELECT 1 FROM public.\"TestDriveOutcomes\" WHERE \"BookingId\" = booking.\"Id\")", restoring);
            ReplaceFunction(migrationBuilder, "public.guard_test_drive_assignment()", "clock_timestamp() >=",
                "NOT EXISTS (SELECT 1 FROM public.\"TestDriveStarts\" WHERE \"BookingId\" = booking.\"Id\") AND clock_timestamp() >=", restoring);
            const string release = "IF NEW.\"Kind\" = 'Released' AND actor.\"Role\" = 'SalesAgent' AND";
            const string activeHandoff = """
                IF EXISTS (SELECT 1 FROM public."TestDriveStarts" WHERE "BookingId" = booking."Id") AND
                    (NEW."Kind" <> 'Assigned' OR actor."Role" <> 'BranchManager' OR NEW."Reason" IS NULL) THEN
                    RAISE EXCEPTION 'Active drives require a reasoned manager handoff'
                        USING ERRCODE = '23514', CONSTRAINT = 'CK_TestDriveAssignments_Active';
                END IF;
                """;
            ReplaceFunction(migrationBuilder, "public.guard_test_drive_assignment()", release, activeHandoff + "\n" + release, restoring);
            const string bookedInterval = "booking.\"StartsAt\", booking.\"EndsAt\"";
            const string activeInterval = "CASE WHEN EXISTS (SELECT 1 FROM public.\"TestDriveStarts\" WHERE \"BookingId\" = booking.\"Id\") THEN clock_timestamp() ELSE booking.\"StartsAt\" END, CASE WHEN EXISTS (SELECT 1 FROM public.\"TestDriveStarts\" WHERE \"BookingId\" = booking.\"Id\") THEN clock_timestamp() + interval '30 minutes' ELSE booking.\"EndsAt\" END";
            ReplaceFunction(migrationBuilder, "public.guard_test_drive_assignment()", bookedInterval, activeInterval, restoring);
        }

        private static void ReplaceFunction(MigrationBuilder migrationBuilder, string function, string original,
            string replacement, bool restoring)
        {
            var before = (restoring ? replacement : original).Replace("'", "''");
            var after = (restoring ? original : replacement).Replace("'", "''");
            migrationBuilder.Sql($"""
                DO $migration$
                DECLARE definition text := pg_get_functiondef('{function}'::regprocedure);
                BEGIN
                    IF (length(definition) - length(replace(definition, '{before}', ''))) / length('{before}') <> 1 THEN
                        RAISE EXCEPTION 'Unexpected function definition for {function}';
                    END IF;
                    EXECUTE replace(definition, '{before}', '{after}');
                END $migration$;
                """);
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dealership.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InspectionVehicleLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(name: "CK_Vehicles_State", table: "Vehicles");
            migrationBuilder.AddCheckConstraint(
                name: "CK_Vehicles_State",
                table: "Vehicles",
                sql: "\"MileageKilometres\" >= 0 AND \"Status\" IN ('PendingInspection', 'Rejected', 'Available') AND \"ManufactureYear\" > 0");
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION guard_inspection_reopening() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE
                    actor "AspNetUsers"%ROWTYPE;
                    draft "InspectionDrafts"%ROWTYPE;
                    signed "InspectionSignedRevisions"%ROWTYPE;
                    vehicle_id uuid;
                    latest_version bigint;
                BEGIN
                    IF TG_OP <> 'INSERT' THEN
                        RAISE EXCEPTION 'Inspection reopening receipts are immutable'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionReopenings_Immutable';
                    END IF;
                    SELECT * INTO actor FROM "AspNetUsers" WHERE "Id" = NEW."ActorId" FOR SHARE;
                    PERFORM 1 FROM "Branches" WHERE "Id" = NEW."BranchId" AND "IsActive" FOR SHARE;
                    IF NOT FOUND OR actor."Id" IS NULL OR NOT actor."IsActive" OR
                       actor."Role" <> 'BranchManager' OR actor."BranchId" IS DISTINCT FROM NEW."BranchId" THEN
                        RAISE EXCEPTION 'Reopening requires a current active branch manager'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionReopenings_Eligibility';
                    END IF;
                    SELECT "VehicleId" INTO vehicle_id FROM "InspectionDrafts" WHERE "Id" = NEW."InspectionId";
                    PERFORM 1 FROM "Vehicles" WHERE "Id" = vehicle_id AND "BranchId" = NEW."BranchId"
                        AND "Status" IN ('PendingInspection', 'Rejected', 'Available') FOR UPDATE;
                    IF NOT FOUND THEN
                        RAISE EXCEPTION 'Reopening requires a matching eligible vehicle'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionReopenings_Vehicle';
                    END IF;
                    SELECT * INTO draft FROM "InspectionDrafts" WHERE "Id" = NEW."InspectionId" FOR UPDATE;
                    SELECT * INTO signed FROM "InspectionSignedRevisions" WHERE "Id" = NEW."SignedRevisionId";
                    SELECT max("Version") INTO latest_version FROM "InspectionDraftSaves" WHERE "InspectionId" = NEW."InspectionId";
                    IF draft."Id" IS NULL OR NOT draft."IsSigned" OR draft."BranchId" <> NEW."BranchId" OR
                       draft."Revision" <> NEW."ExpectedRevision" OR draft."AssignmentVersion" <> NEW."AssignmentVersion" OR
                       signed."Id" IS NULL OR signed."InspectionId" <> draft."Id" OR signed."Revision" <> draft."Revision" OR
                       NEW."AfterSaveVersion" IS DISTINCT FROM latest_version OR
                       NEW."RecordedAt" < signed."SignedAt" OR NEW."RecordedAt" > clock_timestamp() THEN
                        RAISE EXCEPTION 'Reopening must retain the exact current signed revision and save boundary'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionReopenings_Revision';
                    END IF;
                    UPDATE "InspectionDrafts" SET "IsSigned" = false, "Revision" = NEW."Revision",
                        "ReopenedAfterSaveVersion" = NEW."AfterSaveVersion" WHERE "Id" = draft."Id";
                    RETURN NEW;
                END $$;
                CREATE OR REPLACE FUNCTION guard_inspection_repair_request() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE
                    actor "AspNetUsers"%ROWTYPE;
                    draft "InspectionDrafts"%ROWTYPE;
                    signed "InspectionSignedRevisions"%ROWTYPE;
                    vehicle_id uuid;
                BEGIN
                    IF TG_OP <> 'INSERT' THEN
                        RAISE EXCEPTION 'Repair requests are retained immutable evidence'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionRepairRequests_Immutable';
                    END IF;
                    SELECT * INTO actor FROM "AspNetUsers" WHERE "Id" = NEW."ActorId" FOR SHARE;
                    PERFORM 1 FROM "Branches" WHERE "Id" = NEW."BranchId" AND "IsActive" FOR SHARE;
                    IF NOT FOUND OR actor."Id" IS NULL OR NOT actor."IsActive" OR
                       actor."Role" <> 'WorkshopTechnician' OR actor."BranchId" IS DISTINCT FROM NEW."BranchId" THEN
                        RAISE EXCEPTION 'Repair requests require a current active branch technician'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionRepairRequests_Eligibility';
                    END IF;
                    SELECT "VehicleId" INTO vehicle_id FROM "InspectionDrafts" WHERE "Id" = NEW."InspectionId";
                    PERFORM 1 FROM "Vehicles" WHERE "Id" = vehicle_id AND "BranchId" = NEW."BranchId"
                        AND "Status" IN ('PendingInspection', 'Available') FOR UPDATE;
                    IF NOT FOUND THEN
                        RAISE EXCEPTION 'Repair requests require an eligible matching vehicle'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionRepairRequests_Vehicle';
                    END IF;
                    SELECT * INTO draft FROM "InspectionDrafts" WHERE "Id" = NEW."InspectionId" FOR UPDATE;
                    SELECT * INTO signed FROM "InspectionSignedRevisions" WHERE "Id" = NEW."SignedRevisionId";
                    IF draft."Id" IS NULL OR NOT draft."IsSigned" OR draft."BranchId" <> NEW."BranchId" OR
                       draft."TechnicianId" <> NEW."ActorId" OR draft."AssignmentVersion" <> NEW."AssignmentVersion" OR
                       draft."Revision" <> NEW."Revision" OR draft."RepairVersion" <> NEW."ExpectedVersion" OR
                       signed."Id" IS NULL OR signed."InspectionId" <> draft."Id" OR signed."Revision" <> draft."Revision" OR
                       signed."ReportJson"->'assessment'->'isRejected' IS DISTINCT FROM 'false'::jsonb OR
                       NEW."RequestedAt" < signed."SignedAt" OR NEW."RequestedAt" > clock_timestamp() THEN
                        RAISE EXCEPTION 'Repair request must select the current non-rejected signed revision and repair version'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionRepairRequests_Revision';
                    END IF;
                    IF NOT valid_inspection_repair_items(NEW."ItemsJson") THEN
                        RAISE EXCEPTION 'Repair work requires a bounded unique finding scope'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionRepairRequests_Items';
                    END IF;
                    IF EXISTS (SELECT 1 FROM jsonb_array_elements(NEW."ItemsJson") item WHERE NOT EXISTS (
                        SELECT 1 FROM jsonb_array_elements(signed."ReportJson"->'checklist') point
                        WHERE point->>'code' = item->>'pointCode' AND point->'kind' = '0'::jsonb)) THEN
                        RAISE EXCEPTION 'Repair scope must reference findings in its selected signed report'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionRepairRequests_Findings';
                    END IF;
                    UPDATE "InspectionDrafts" SET "RepairVersion" = NEW."Version" WHERE "Id" = draft."Id";
                    RETURN NEW;
                END $$;
                CREATE OR REPLACE FUNCTION guard_inspection_repair_completion() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE
                    actor "AspNetUsers"%ROWTYPE;
                    draft "InspectionDrafts"%ROWTYPE;
                    request "InspectionRepairRequests"%ROWTYPE;
                    approval "InspectionRepairApprovals"%ROWTYPE;
                    approved_request "InspectionRepairRequests"%ROWTYPE;
                    vehicle_id uuid;
                BEGIN
                    IF TG_OP <> 'INSERT' THEN
                        RAISE EXCEPTION 'Repair completions are retained immutable evidence'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionRepairCompletions_Immutable';
                    END IF;
                    SELECT * INTO actor FROM "AspNetUsers" WHERE "Id" = NEW."ActorId" FOR SHARE;
                    PERFORM 1 FROM "Branches" WHERE "Id" = NEW."BranchId" AND "IsActive" FOR SHARE;
                    IF NOT FOUND OR actor."Id" IS NULL OR NOT actor."IsActive" OR
                       actor."Role" <> 'WorkshopTechnician' OR actor."BranchId" IS DISTINCT FROM NEW."BranchId" THEN
                        RAISE EXCEPTION 'Completion requires a current active same-branch technician'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionRepairCompletions_Eligibility';
                    END IF;
                    SELECT "VehicleId" INTO vehicle_id FROM "InspectionDrafts" WHERE "Id" = NEW."InspectionId";
                    PERFORM 1 FROM "Vehicles" WHERE "Id" = vehicle_id AND "BranchId" = NEW."BranchId"
                        AND "Status" IN ('PendingInspection', 'Available') FOR UPDATE;
                    IF NOT FOUND THEN
                        RAISE EXCEPTION 'Completion requires an eligible matching vehicle'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionRepairCompletions_Vehicle';
                    END IF;
                    SELECT * INTO draft FROM "InspectionDrafts" WHERE "Id" = NEW."InspectionId" FOR UPDATE;
                    SELECT * INTO request FROM "InspectionRepairRequests" WHERE "Id" = NEW."RequestId";
                    IF draft."Id" IS NULL OR NOT draft."IsSigned" OR draft."TechnicianId" <> NEW."ActorId" OR
                       draft."BranchId" <> NEW."BranchId" OR draft."AssignmentVersion" <> NEW."AssignmentVersion" OR
                       draft."Revision" <> NEW."Revision" OR draft."RepairVersion" <> NEW."RepairVersion" OR
                       request."Id" IS NULL OR request."InspectionId" <> draft."Id" OR request."BranchId" <> draft."BranchId" OR
                       request."AssignmentVersion" <> NEW."AssignmentVersion" OR request."Revision" <> NEW."Revision" OR
                       request."Version" <> NEW."RepairVersion" OR NEW."CompletedAt" < request."RequestedAt" OR
                       NEW."CompletedAt" > clock_timestamp() THEN
                        RAISE EXCEPTION 'Completion must retain the exact current assigned signed repair request'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionRepairCompletions_Request';
                    END IF;
                    IF request."Budget" = 0 THEN
                        IF NEW."ApprovalId" IS NOT NULL THEN
                            RAISE EXCEPTION 'Zero budget completion requires no approval reference'
                                USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionRepairCompletions_Approval';
                        END IF;
                        RETURN NEW;
                    END IF;
                    SELECT * INTO approval FROM "InspectionRepairApprovals" WHERE "Id" = NEW."ApprovalId";
                    SELECT * INTO approved_request FROM "InspectionRepairRequests" WHERE "Id" = approval."RequestId";
                    IF approval."Id" IS NULL OR approved_request."Id" IS NULL OR
                       approval."InspectionId" <> draft."Id" OR approval."BranchId" <> draft."BranchId" OR
                       approval."Revision" <> NEW."Revision" OR approved_request."ItemsJson" IS DISTINCT FROM request."ItemsJson" OR
                       approved_request."Budget" < request."Budget" OR NEW."CompletedAt" < approval."ApprovedAt" THEN
                        RAISE EXCEPTION 'Positive budget completion requires retained covering manager approval'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionRepairCompletions_Approval';
                    END IF;
                    RETURN NEW;
                END $$;

                CREATE FUNCTION inspection_vehicle_status(vehicle_id uuid) RETURNS text LANGUAGE plpgsql AS $$
                DECLARE
                    draft "InspectionDrafts"%ROWTYPE;
                    report jsonb;
                    request "InspectionRepairRequests"%ROWTYPE;
                BEGIN
                    SELECT * INTO draft FROM "InspectionDrafts" WHERE "VehicleId" = vehicle_id;
                    IF draft."Id" IS NULL OR NOT draft."IsSigned" THEN
                        RETURN 'PendingInspection';
                    END IF;
                    SELECT "ReportJson" INTO report FROM "InspectionSignedRevisions"
                        WHERE "InspectionId" = draft."Id" AND "Revision" = draft."Revision";
                    IF report IS NULL THEN
                        RETURN 'PendingInspection';
                    END IF;
                    IF report->'assessment'->'isRejected' = 'true'::jsonb THEN
                        RETURN 'Rejected';
                    END IF;
                    SELECT * INTO request FROM "InspectionRepairRequests" WHERE "InspectionId" = draft."Id"
                        AND "Revision" = draft."Revision" AND "Version" = draft."RepairVersion";
                    IF request."Id" IS NOT NULL AND NOT EXISTS (SELECT 1 FROM "InspectionRepairCompletions" c
                        WHERE c."RequestId" = request."Id" AND c."InspectionId" = draft."Id" AND
                            c."Revision" = draft."Revision" AND c."AssignmentVersion" = draft."AssignmentVersion" AND
                            c."RepairVersion" = draft."RepairVersion") THEN
                        RETURN 'PendingInspection';
                    END IF;
                    -- Every required finding must be in the completed current request, even when its estimate is zero.
                    IF EXISTS (SELECT 1 FROM jsonb_array_elements(report->'checklist') point
                        WHERE point->'kind' = '0'::jsonb AND
                            (point->'severity' IN ('1'::jsonb, '2'::jsonb) OR (point->>'estimatedRepairCost')::numeric > 0) AND
                            NOT EXISTS (SELECT 1 FROM jsonb_array_elements(request."ItemsJson") item
                                WHERE item->>'pointCode' = point->>'code')) THEN
                        RETURN 'PendingInspection';
                    END IF;
                    RETURN 'Available';
                END $$;

                CREATE FUNCTION apply_inspection_vehicle_status() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE
                    vehicle_id uuid;
                BEGIN
                    SELECT "VehicleId" INTO vehicle_id FROM "InspectionDrafts" WHERE "Id" = NEW."InspectionId";
                    -- Updating the locked vehicle also fences older repeatable-read writers, including no-op status changes.
                    UPDATE "Vehicles" SET "Status" = inspection_vehicle_status(vehicle_id) WHERE "Id" = vehicle_id;
                    RETURN NULL;
                END $$;
                CREATE TRIGGER inspection_signed_vehicle_status AFTER INSERT ON "InspectionSignedRevisions"
                    FOR EACH ROW EXECUTE FUNCTION apply_inspection_vehicle_status();
                CREATE TRIGGER inspection_reopened_vehicle_status AFTER INSERT ON "InspectionReopenings"
                    FOR EACH ROW EXECUTE FUNCTION apply_inspection_vehicle_status();
                CREATE TRIGGER inspection_requested_vehicle_status AFTER INSERT ON "InspectionRepairRequests"
                    FOR EACH ROW EXECUTE FUNCTION apply_inspection_vehicle_status();
                CREATE TRIGGER inspection_completed_vehicle_status AFTER INSERT ON "InspectionRepairCompletions"
                    FOR EACH ROW EXECUTE FUNCTION apply_inspection_vehicle_status();

                CREATE FUNCTION require_inspection_vehicle_status() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE
                    vehicle_id uuid;
                BEGIN
                    vehicle_id := NEW."Id";
                    IF TG_TABLE_NAME = 'InspectionDrafts' THEN
                        vehicle_id := NEW."VehicleId";
                    END IF;
                    IF EXISTS (SELECT 1 FROM "Vehicles" v WHERE v."Id" = vehicle_id AND
                        v."Status" IS DISTINCT FROM inspection_vehicle_status(vehicle_id)) THEN
                        RAISE EXCEPTION 'Vehicle status requires the current signed report and complete repair evidence'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_Vehicles_InspectionLifecycle';
                    END IF;
                    RETURN NULL;
                END $$;
                CREATE CONSTRAINT TRIGGER vehicle_inspection_lifecycle AFTER INSERT OR UPDATE ON "Vehicles"
                    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION require_inspection_vehicle_status();
                CREATE CONSTRAINT TRIGGER draft_inspection_lifecycle AFTER INSERT OR UPDATE ON "InspectionDrafts"
                    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION require_inspection_vehicle_status();

                -- Derive existing rows from retained evidence; never alter signed reports or intake provenance.
                UPDATE "Vehicles" SET "Status" = inspection_vehicle_status("Id");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER draft_inspection_lifecycle ON "InspectionDrafts";
                DROP TRIGGER vehicle_inspection_lifecycle ON "Vehicles";
                DROP FUNCTION require_inspection_vehicle_status();
                DROP TRIGGER inspection_signed_vehicle_status ON "InspectionSignedRevisions";
                DROP TRIGGER inspection_reopened_vehicle_status ON "InspectionReopenings";
                DROP TRIGGER inspection_requested_vehicle_status ON "InspectionRepairRequests";
                DROP TRIGGER inspection_completed_vehicle_status ON "InspectionRepairCompletions";
                DROP FUNCTION apply_inspection_vehicle_status();
                DROP FUNCTION inspection_vehicle_status(uuid);
                CREATE OR REPLACE FUNCTION guard_inspection_repair_completion() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE
                    actor "AspNetUsers"%ROWTYPE;
                    draft "InspectionDrafts"%ROWTYPE;
                    request "InspectionRepairRequests"%ROWTYPE;
                    approval "InspectionRepairApprovals"%ROWTYPE;
                    approved_request "InspectionRepairRequests"%ROWTYPE;
                    vehicle_id uuid;
                BEGIN
                    IF TG_OP <> 'INSERT' THEN
                        RAISE EXCEPTION 'Repair completions are retained immutable evidence'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionRepairCompletions_Immutable';
                    END IF;
                    SELECT * INTO actor FROM "AspNetUsers" WHERE "Id" = NEW."ActorId" FOR SHARE;
                    PERFORM 1 FROM "Branches" WHERE "Id" = NEW."BranchId" AND "IsActive" FOR SHARE;
                    IF NOT FOUND OR actor."Id" IS NULL OR NOT actor."IsActive" OR
                       actor."Role" <> 'WorkshopTechnician' OR actor."BranchId" IS DISTINCT FROM NEW."BranchId" THEN
                        RAISE EXCEPTION 'Completion requires a current active same-branch technician'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionRepairCompletions_Eligibility';
                    END IF;
                    SELECT "VehicleId" INTO vehicle_id FROM "InspectionDrafts" WHERE "Id" = NEW."InspectionId";
                    PERFORM 1 FROM "Vehicles" WHERE "Id" = vehicle_id AND "BranchId" = NEW."BranchId"
                        AND "Status" = 'PendingInspection' FOR UPDATE;
                    IF NOT FOUND THEN
                        RAISE EXCEPTION 'Completion requires an eligible matching vehicle'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionRepairCompletions_Vehicle';
                    END IF;
                    SELECT * INTO draft FROM "InspectionDrafts" WHERE "Id" = NEW."InspectionId" FOR UPDATE;
                    SELECT * INTO request FROM "InspectionRepairRequests" WHERE "Id" = NEW."RequestId";
                    IF draft."Id" IS NULL OR NOT draft."IsSigned" OR draft."TechnicianId" <> NEW."ActorId" OR
                       draft."BranchId" <> NEW."BranchId" OR draft."AssignmentVersion" <> NEW."AssignmentVersion" OR
                       draft."Revision" <> NEW."Revision" OR draft."RepairVersion" <> NEW."RepairVersion" OR
                       request."Id" IS NULL OR request."InspectionId" <> draft."Id" OR request."BranchId" <> draft."BranchId" OR
                       request."AssignmentVersion" <> NEW."AssignmentVersion" OR request."Revision" <> NEW."Revision" OR
                       request."Version" <> NEW."RepairVersion" OR NEW."CompletedAt" < request."RequestedAt" OR
                       NEW."CompletedAt" > clock_timestamp() THEN
                        RAISE EXCEPTION 'Completion must retain the exact current assigned signed repair request'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionRepairCompletions_Request';
                    END IF;
                    IF request."Budget" = 0 THEN
                        IF NEW."ApprovalId" IS NOT NULL THEN
                            RAISE EXCEPTION 'Zero budget completion requires no approval reference'
                                USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionRepairCompletions_Approval';
                        END IF;
                        RETURN NEW;
                    END IF;
                    SELECT * INTO approval FROM "InspectionRepairApprovals" WHERE "Id" = NEW."ApprovalId";
                    SELECT * INTO approved_request FROM "InspectionRepairRequests" WHERE "Id" = approval."RequestId";
                    IF approval."Id" IS NULL OR approved_request."Id" IS NULL OR
                       approval."InspectionId" <> draft."Id" OR approval."BranchId" <> draft."BranchId" OR
                       approval."Revision" <> NEW."Revision" OR approved_request."ItemsJson" IS DISTINCT FROM request."ItemsJson" OR
                       approved_request."Budget" < request."Budget" OR NEW."CompletedAt" < approval."ApprovedAt" THEN
                        RAISE EXCEPTION 'Positive budget completion requires retained covering manager approval'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionRepairCompletions_Approval';
                    END IF;
                    RETURN NEW;
                END $$;
                UPDATE "Vehicles" SET "Status" = 'PendingInspection';
                CREATE OR REPLACE FUNCTION guard_inspection_reopening() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE
                    actor "AspNetUsers"%ROWTYPE;
                    draft "InspectionDrafts"%ROWTYPE;
                    signed "InspectionSignedRevisions"%ROWTYPE;
                    vehicle_id uuid;
                    latest_version bigint;
                BEGIN
                    IF TG_OP <> 'INSERT' THEN
                        RAISE EXCEPTION 'Inspection reopening receipts are immutable'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionReopenings_Immutable';
                    END IF;
                    SELECT * INTO actor FROM "AspNetUsers" WHERE "Id" = NEW."ActorId" FOR SHARE;
                    PERFORM 1 FROM "Branches" WHERE "Id" = NEW."BranchId" AND "IsActive" FOR SHARE;
                    IF NOT FOUND OR actor."Id" IS NULL OR NOT actor."IsActive" OR
                       actor."Role" <> 'BranchManager' OR actor."BranchId" IS DISTINCT FROM NEW."BranchId" THEN
                        RAISE EXCEPTION 'Reopening requires a current active branch manager'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionReopenings_Eligibility';
                    END IF;
                    SELECT "VehicleId" INTO vehicle_id FROM "InspectionDrafts" WHERE "Id" = NEW."InspectionId";
                    PERFORM 1 FROM "Vehicles" WHERE "Id" = vehicle_id AND "BranchId" = NEW."BranchId"
                        AND "Status" = 'PendingInspection' FOR UPDATE;
                    IF NOT FOUND THEN
                        RAISE EXCEPTION 'Reopening requires a matching eligible vehicle'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionReopenings_Vehicle';
                    END IF;
                    SELECT * INTO draft FROM "InspectionDrafts" WHERE "Id" = NEW."InspectionId" FOR UPDATE;
                    SELECT * INTO signed FROM "InspectionSignedRevisions" WHERE "Id" = NEW."SignedRevisionId";
                    SELECT max("Version") INTO latest_version FROM "InspectionDraftSaves" WHERE "InspectionId" = NEW."InspectionId";
                    IF draft."Id" IS NULL OR NOT draft."IsSigned" OR draft."BranchId" <> NEW."BranchId" OR
                       draft."Revision" <> NEW."ExpectedRevision" OR draft."AssignmentVersion" <> NEW."AssignmentVersion" OR
                       signed."Id" IS NULL OR signed."InspectionId" <> draft."Id" OR signed."Revision" <> draft."Revision" OR
                       NEW."AfterSaveVersion" IS DISTINCT FROM latest_version OR
                       NEW."RecordedAt" < signed."SignedAt" OR NEW."RecordedAt" > clock_timestamp() THEN
                        RAISE EXCEPTION 'Reopening must retain the exact current signed revision and save boundary'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionReopenings_Revision';
                    END IF;
                    UPDATE "InspectionDrafts" SET "IsSigned" = false, "Revision" = NEW."Revision",
                        "ReopenedAfterSaveVersion" = NEW."AfterSaveVersion" WHERE "Id" = draft."Id";
                    RETURN NEW;
                END $$;
                CREATE OR REPLACE FUNCTION guard_inspection_repair_request() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE
                    actor "AspNetUsers"%ROWTYPE;
                    draft "InspectionDrafts"%ROWTYPE;
                    signed "InspectionSignedRevisions"%ROWTYPE;
                    vehicle_id uuid;
                BEGIN
                    IF TG_OP <> 'INSERT' THEN
                        RAISE EXCEPTION 'Repair requests are retained immutable evidence'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionRepairRequests_Immutable';
                    END IF;
                    SELECT * INTO actor FROM "AspNetUsers" WHERE "Id" = NEW."ActorId" FOR SHARE;
                    PERFORM 1 FROM "Branches" WHERE "Id" = NEW."BranchId" AND "IsActive" FOR SHARE;
                    IF NOT FOUND OR actor."Id" IS NULL OR NOT actor."IsActive" OR
                       actor."Role" <> 'WorkshopTechnician' OR actor."BranchId" IS DISTINCT FROM NEW."BranchId" THEN
                        RAISE EXCEPTION 'Repair requests require a current active branch technician'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionRepairRequests_Eligibility';
                    END IF;
                    SELECT "VehicleId" INTO vehicle_id FROM "InspectionDrafts" WHERE "Id" = NEW."InspectionId";
                    PERFORM 1 FROM "Vehicles" WHERE "Id" = vehicle_id AND "BranchId" = NEW."BranchId"
                        AND "Status" = 'PendingInspection' FOR UPDATE;
                    IF NOT FOUND THEN
                        RAISE EXCEPTION 'Repair requests require an eligible matching vehicle'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionRepairRequests_Vehicle';
                    END IF;
                    SELECT * INTO draft FROM "InspectionDrafts" WHERE "Id" = NEW."InspectionId" FOR UPDATE;
                    SELECT * INTO signed FROM "InspectionSignedRevisions" WHERE "Id" = NEW."SignedRevisionId";
                    IF draft."Id" IS NULL OR NOT draft."IsSigned" OR draft."BranchId" <> NEW."BranchId" OR
                       draft."TechnicianId" <> NEW."ActorId" OR draft."AssignmentVersion" <> NEW."AssignmentVersion" OR
                       draft."Revision" <> NEW."Revision" OR draft."RepairVersion" <> NEW."ExpectedVersion" OR
                       signed."Id" IS NULL OR signed."InspectionId" <> draft."Id" OR signed."Revision" <> draft."Revision" OR
                       signed."ReportJson"->'assessment'->'isRejected' IS DISTINCT FROM 'false'::jsonb OR
                       NEW."RequestedAt" < signed."SignedAt" OR NEW."RequestedAt" > clock_timestamp() THEN
                        RAISE EXCEPTION 'Repair request must select the current non-rejected signed revision and repair version'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionRepairRequests_Revision';
                    END IF;
                    IF NOT valid_inspection_repair_items(NEW."ItemsJson") THEN
                        RAISE EXCEPTION 'Repair work requires a bounded unique finding scope'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionRepairRequests_Items';
                    END IF;
                    IF EXISTS (SELECT 1 FROM jsonb_array_elements(NEW."ItemsJson") item WHERE NOT EXISTS (
                        SELECT 1 FROM jsonb_array_elements(signed."ReportJson"->'checklist') point
                        WHERE point->>'code' = item->>'pointCode' AND point->'kind' = '0'::jsonb)) THEN
                        RAISE EXCEPTION 'Repair scope must reference findings in its selected signed report'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionRepairRequests_Findings';
                    END IF;
                    UPDATE "InspectionDrafts" SET "RepairVersion" = NEW."Version" WHERE "Id" = draft."Id";
                    RETURN NEW;
                END $$;
                """);
            migrationBuilder.DropCheckConstraint(name: "CK_Vehicles_State", table: "Vehicles");
            migrationBuilder.AddCheckConstraint(
                name: "CK_Vehicles_State",
                table: "Vehicles",
                sql: "\"MileageKilometres\" >= 0 AND \"Status\" = 'PendingInspection' AND \"ManufactureYear\" > 0");
        }
    }
}
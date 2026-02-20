using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dealership.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InspectionRepairCompletions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE FUNCTION guard_inspection_repair_completion() RETURNS trigger LANGUAGE plpgsql AS $$
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
                """);
            migrationBuilder.CreateTable(
                name: "InspectionRepairCompletions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InspectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovalId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssignmentVersion = table.Column<long>(type: "bigint", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    RepairVersion = table.Column<long>(type: "bigint", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InspectionRepairCompletions", x => x.Id);
                    table.CheckConstraint("CK_InspectionRepairCompletions_Notes", "\"Notes\" ~ '[^[:space:]]' AND \"Notes\" !~ '[[:cntrl:]]' AND length(\"Notes\") <= 1000");
                    table.CheckConstraint("CK_InspectionRepairCompletions_Shape", "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"AssignmentVersion\" > 0 AND \"Revision\" > 0 AND \"RepairVersion\" > 0 AND isfinite(\"CompletedAt\")");
                    table.ForeignKey(
                        name: "FK_InspectionRepairCompletions_AspNetUsers_ActorId",
                        column: x => x.ActorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InspectionRepairCompletions_InspectionDrafts_InspectionId_B~",
                        columns: x => new { x.InspectionId, x.BranchId },
                        principalTable: "InspectionDrafts",
                        principalColumns: ["Id", "BranchId"],
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InspectionRepairCompletions_InspectionRepairApprovals_Appro~",
                        column: x => x.ApprovalId,
                        principalTable: "InspectionRepairApprovals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InspectionRepairCompletions_InspectionRepairRequests_Reques~",
                        column: x => x.RequestId,
                        principalTable: "InspectionRepairRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InspectionRepairCompletions_ActorId",
                table: "InspectionRepairCompletions",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionRepairCompletions_ApprovalId",
                table: "InspectionRepairCompletions",
                column: "ApprovalId");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionRepairCompletions_InspectionId_BranchId",
                table: "InspectionRepairCompletions",
                columns: ["InspectionId", "BranchId"]);

            migrationBuilder.CreateIndex(
                name: "IX_InspectionRepairCompletions_InspectionId_RepairVersion",
                table: "InspectionRepairCompletions",
                columns: ["InspectionId", "RepairVersion"],
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InspectionRepairCompletions_RequestId",
                table: "InspectionRepairCompletions",
                column: "RequestId",
                unique: true);
            migrationBuilder.Sql("""
                CREATE TRIGGER inspection_repair_completion_guard BEFORE INSERT OR UPDATE OR DELETE ON "InspectionRepairCompletions"
                    FOR EACH ROW EXECUTE FUNCTION guard_inspection_repair_completion();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER inspection_repair_completion_guard ON "InspectionRepairCompletions";
                DROP FUNCTION guard_inspection_repair_completion();
                """);
            migrationBuilder.DropTable(
                name: "InspectionRepairCompletions");
        }
    }
}

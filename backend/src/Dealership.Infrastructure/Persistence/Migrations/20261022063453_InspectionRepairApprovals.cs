using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dealership.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InspectionRepairApprovals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InspectionRepairApprovals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InspectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignmentVersion = table.Column<long>(type: "bigint", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    RepairVersion = table.Column<long>(type: "bigint", nullable: false),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InspectionRepairApprovals", x => x.Id);
                    table.CheckConstraint("CK_InspectionRepairApprovals_Shape", "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"AssignmentVersion\" > 0 AND \"Revision\" > 0 AND \"RepairVersion\" > 0 AND isfinite(\"ApprovedAt\")");
                    table.ForeignKey(
                        name: "FK_InspectionRepairApprovals_AspNetUsers_ActorId",
                        column: x => x.ActorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InspectionRepairApprovals_InspectionDrafts_InspectionId_Bra~",
                        columns: x => new { x.InspectionId, x.BranchId },
                        principalTable: "InspectionDrafts",
                        principalColumns: ["Id", "BranchId"],
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InspectionRepairApprovals_InspectionRepairRequests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "InspectionRepairRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InspectionRepairApprovals_ActorId",
                table: "InspectionRepairApprovals",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionRepairApprovals_InspectionId_BranchId",
                table: "InspectionRepairApprovals",
                columns: ["InspectionId", "BranchId"]);

            migrationBuilder.CreateIndex(
                name: "IX_InspectionRepairApprovals_InspectionId_RepairVersion",
                table: "InspectionRepairApprovals",
                columns: ["InspectionId", "RepairVersion"],
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InspectionRepairApprovals_RequestId",
                table: "InspectionRepairApprovals",
                column: "RequestId",
                unique: true);
            migrationBuilder.Sql("""
                CREATE FUNCTION guard_inspection_repair_approval() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE
                    actor "AspNetUsers"%ROWTYPE;
                    draft "InspectionDrafts"%ROWTYPE;
                    request "InspectionRepairRequests"%ROWTYPE;
                    vehicle_id uuid;
                BEGIN
                    IF TG_OP <> 'INSERT' THEN
                        RAISE EXCEPTION 'Repair approvals are retained immutable evidence'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionRepairApprovals_Immutable';
                    END IF;
                    SELECT * INTO actor FROM "AspNetUsers" WHERE "Id" = NEW."ActorId" FOR SHARE;
                    PERFORM 1 FROM "Branches" WHERE "Id" = NEW."BranchId" AND "IsActive" FOR SHARE;
                    IF NOT FOUND OR actor."Id" IS NULL OR NOT actor."IsActive" OR
                       actor."Role" <> 'BranchManager' OR actor."BranchId" IS DISTINCT FROM NEW."BranchId" THEN
                        RAISE EXCEPTION 'Repair approvals require a current active same-branch manager'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionRepairApprovals_Eligibility';
                    END IF;
                    SELECT "VehicleId" INTO vehicle_id FROM "InspectionDrafts" WHERE "Id" = NEW."InspectionId";
                    PERFORM 1 FROM "Vehicles" WHERE "Id" = vehicle_id AND "BranchId" = NEW."BranchId"
                        AND "Status" = 'PendingInspection' FOR UPDATE;
                    IF NOT FOUND THEN
                        RAISE EXCEPTION 'Repair approvals require an eligible matching vehicle'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionRepairApprovals_Vehicle';
                    END IF;
                    SELECT * INTO draft FROM "InspectionDrafts" WHERE "Id" = NEW."InspectionId" FOR UPDATE;
                    SELECT * INTO request FROM "InspectionRepairRequests" WHERE "Id" = NEW."RequestId";
                    IF draft."Id" IS NULL OR NOT draft."IsSigned" OR draft."BranchId" <> NEW."BranchId" OR
                       draft."AssignmentVersion" <> NEW."AssignmentVersion" OR draft."Revision" <> NEW."Revision" OR
                       draft."RepairVersion" <> NEW."RepairVersion" OR request."Id" IS NULL OR
                       request."InspectionId" <> draft."Id" OR request."BranchId" <> draft."BranchId" OR
                       request."AssignmentVersion" <> NEW."AssignmentVersion" OR request."Revision" <> NEW."Revision" OR
                       request."Version" <> NEW."RepairVersion" OR NEW."ApprovedAt" < request."RequestedAt" OR
                       NEW."ApprovedAt" > clock_timestamp() THEN
                        RAISE EXCEPTION 'Approval must retain the exact current signed repair request'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionRepairApprovals_Request';
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER inspection_repair_approval_guard BEFORE INSERT OR UPDATE OR DELETE ON "InspectionRepairApprovals"
                    FOR EACH ROW EXECUTE FUNCTION guard_inspection_repair_approval();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER inspection_repair_approval_guard ON "InspectionRepairApprovals";
                DROP FUNCTION guard_inspection_repair_approval();
                """);
            migrationBuilder.DropTable(
                name: "InspectionRepairApprovals");
        }
    }
}

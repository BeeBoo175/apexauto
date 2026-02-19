using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dealership.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InspectionSignedRevisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_InspectionDrafts_Shape",
                table: "InspectionDrafts");

            migrationBuilder.AddColumn<bool>(
                name: "IsSigned",
                table: "InspectionDrafts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "Revision",
                table: "InspectionDrafts",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.CreateTable(
                name: "InspectionSignedRevisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InspectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    SaveId = table.Column<Guid>(type: "uuid", nullable: false),
                    TechnicianId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignmentVersion = table.Column<long>(type: "bigint", nullable: false),
                    SaveVersion = table.Column<long>(type: "bigint", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    SignedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReportJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InspectionSignedRevisions", x => x.Id);
                    table.CheckConstraint("CK_InspectionSignedRevisions_Shape", "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"Revision\" > 0 AND \"AssignmentVersion\" > 0 AND \"SaveVersion\" > 0 AND isfinite(\"SignedAt\") AND jsonb_typeof(\"ReportJson\") = 'object' AND octet_length(\"ReportJson\"::text) <= 4194304");
                    table.ForeignKey(
                        name: "FK_InspectionSignedRevisions_AspNetUsers_TechnicianId",
                        column: x => x.TechnicianId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InspectionSignedRevisions_InspectionDraftSaves_SaveId_Inspe~",
                        columns: x => new { x.SaveId, x.InspectionId },
                        principalTable: "InspectionDraftSaves",
                        principalColumns: ["Id", "InspectionId"],
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InspectionSignedRevisions_InspectionDrafts_InspectionId_Bra~",
                        columns: x => new { x.InspectionId, x.BranchId },
                        principalTable: "InspectionDrafts",
                        principalColumns: ["Id", "BranchId"],
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_InspectionDrafts_Shape",
                table: "InspectionDrafts",
                sql: "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"AssignmentVersion\" > 0 AND \"Revision\" > 0");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionSignedRevisions_InspectionId_BranchId",
                table: "InspectionSignedRevisions",
                columns: ["InspectionId", "BranchId"]);

            migrationBuilder.CreateIndex(
                name: "IX_InspectionSignedRevisions_InspectionId_Revision",
                table: "InspectionSignedRevisions",
                columns: ["InspectionId", "Revision"],
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InspectionSignedRevisions_SaveId",
                table: "InspectionSignedRevisions",
                column: "SaveId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InspectionSignedRevisions_SaveId_InspectionId",
                table: "InspectionSignedRevisions",
                columns: ["SaveId", "InspectionId"]);

            migrationBuilder.CreateIndex(
                name: "IX_InspectionSignedRevisions_TechnicianId",
                table: "InspectionSignedRevisions",
                column: "TechnicianId");

            migrationBuilder.Sql("""
                CREATE FUNCTION inspection_catalog_v1() RETURNS jsonb LANGUAGE sql IMMUTABLE AS $$
                    SELECT jsonb_agg(jsonb_build_object('code', code, 'category', category, 'description', description) ORDER BY ordinal)
                    FROM (VALUES
                    (1, 'FC-001', 0, 'VIN stamping identity and legibility'),
                    (2, 'FC-002', 0, 'Manufacturer identity plate attachment and consistency'),
                    (3, 'FC-003', 0, 'Front left chassis rail shape and alignment'),
                    (4, 'FC-004', 0, 'Front right chassis rail shape and alignment'),
                    (5, 'FC-005', 0, 'Rear left chassis rail shape and alignment'),
                    (6, 'FC-006', 0, 'Rear right chassis rail shape and alignment'),
                    (7, 'FC-007', 0, 'Front crossmember structure'),
                    (8, 'FC-008', 0, 'Rear crossmember structure'),
                    (9, 'FC-009', 0, 'Left front suspension tower structure'),
                    (10, 'FC-010', 0, 'Right front suspension tower structure'),
                    (11, 'FC-011', 0, 'Left rear suspension mounting structure'),
                    (12, 'FC-012', 0, 'Right rear suspension mounting structure'),
                    (13, 'FC-013', 0, 'Left A-pillar structure'),
                    (14, 'FC-014', 0, 'Right A-pillar structure'),
                    (15, 'FC-015', 0, 'Left B-pillar structure'),
                    (16, 'FC-016', 0, 'Right B-pillar structure'),
                    (17, 'FC-017', 0, 'Left C-pillar structure'),
                    (18, 'FC-018', 0, 'Right C-pillar structure'),
                    (19, 'FC-019', 0, 'Left roof rail structure'),
                    (20, 'FC-020', 0, 'Right roof rail structure'),
                    (21, 'FC-021', 0, 'Roof crossmember structure'),
                    (22, 'FC-022', 0, 'Left sill structure'),
                    (23, 'FC-023', 0, 'Right sill structure'),
                    (24, 'FC-024', 0, 'Front bulkhead structure'),
                    (25, 'FC-025', 0, 'Rear bulkhead structure'),
                    (26, 'FC-026', 0, 'Left front floor structure'),
                    (27, 'FC-027', 0, 'Right front floor structure'),
                    (28, 'FC-028', 0, 'Left rear floor structure'),
                    (29, 'FC-029', 0, 'Right rear floor structure'),
                    (30, 'FC-030', 0, 'Spare-wheel well structure'),
                    (31, 'FC-031', 0, 'Front crash reinforcement structure'),
                    (32, 'FC-032', 0, 'Rear crash reinforcement structure'),
                    (33, 'FC-033', 0, 'Left front structural apron'),
                    (34, 'FC-034', 0, 'Right front structural apron'),
                    (35, 'FC-035', 0, 'Left rear inner quarter structure'),
                    (36, 'FC-036', 0, 'Right rear inner quarter structure'),
                    (37, 'FC-037', 0, 'Structural seam and spot-weld consistency'),
                    (38, 'FC-038', 0, 'Evidence of structural cut-and-weld joining'),
                    (39, 'FC-039', 0, 'Structural heat distortion or repair evidence'),
                    (40, 'FC-040', 0, 'Structural corrosion or perforation'),
                    (41, 'FC-041', 0, 'Structural alignment and opening symmetry'),
                    (42, 'FC-042', 0, 'Cabin sediment or waterline evidence'),
                    (43, 'FC-043', 0, 'Enclosed cavity flood contamination evidence'),
                    (44, 'FC-044', 0, 'Safety-belt anchor mounting structure'),
                    (45, 'FC-045', 0, 'Subframe attachment structure'),
                    (46, 'ET-001', 1, 'Engine identity and recorded specification consistency'),
                    (47, 'ET-002', 1, 'Cold-start behavior'),
                    (48, 'ET-003', 1, 'Warm restart behavior'),
                    (49, 'ET-004', 1, 'Idle stability'),
                    (50, 'ET-005', 1, 'Abnormal engine mechanical noise'),
                    (51, 'ET-006', 1, 'Exhaust smoke condition'),
                    (52, 'ET-007', 1, 'Engine oil level and visible condition'),
                    (53, 'ET-008', 1, 'Oil filler contamination evidence'),
                    (54, 'ET-009', 1, 'Engine oil leakage'),
                    (55, 'ET-010', 1, 'Coolant level and visible condition'),
                    (56, 'ET-011', 1, 'Coolant contamination evidence'),
                    (57, 'ET-012', 1, 'Radiator condition'),
                    (58, 'ET-013', 1, 'Cooling hose condition'),
                    (59, 'ET-014', 1, 'Coolant reservoir condition'),
                    (60, 'ET-015', 1, 'Cooling fan operation'),
                    (61, 'ET-016', 1, 'Operating temperature stability'),
                    (62, 'ET-017', 1, 'Visible water-pump leakage'),
                    (63, 'ET-018', 1, 'Accessory belt condition'),
                    (64, 'ET-019', 1, 'Belt tensioner or pulley noise'),
                    (65, 'ET-020', 1, 'Engine mounting condition'),
                    (66, 'ET-021', 1, 'Intake duct and air-filter housing condition'),
                    (67, 'ET-022', 1, 'Throttle response'),
                    (68, 'ET-023', 1, 'Fuel line leakage or deterioration'),
                    (69, 'ET-024', 1, 'Fuel injector area leakage'),
                    (70, 'ET-025', 1, 'Fuel tank and filler sealing condition'),
                    (71, 'ET-026', 1, 'Ignition misfire evidence'),
                    (72, 'ET-027', 1, 'Engine diagnostic fault evidence'),
                    (73, 'ET-028', 1, 'Diagnostic readiness or recently cleared fault evidence'),
                    (74, 'ET-029', 1, 'Charging output condition'),
                    (75, 'ET-030', 1, 'Starter operation'),
                    (76, 'ET-031', 1, 'Visible engine wiring and connector condition'),
                    (77, 'ET-032', 1, 'Exhaust manifold leakage'),
                    (78, 'ET-033', 1, 'Exhaust pipe and silencer condition'),
                    (79, 'ET-034', 1, 'Exhaust mounting condition'),
                    (80, 'ET-035', 1, 'Visible emission-system component condition'),
                    (81, 'ET-036', 1, 'Transmission identity and specification consistency'),
                    (82, 'ET-037', 1, 'Transmission fluid level or condition where serviceable'),
                    (83, 'ET-038', 1, 'Transmission fluid leakage'),
                    (84, 'ET-039', 1, 'Transmission mounting condition'),
                    (85, 'ET-040', 1, 'Gear selector engagement'),
                    (86, 'ET-041', 1, 'Forward drive take-up'),
                    (87, 'ET-042', 1, 'Reverse drive take-up'),
                    (88, 'ET-043', 1, 'Gear change quality'),
                    (89, 'ET-044', 1, 'Transmission slip or shudder evidence'),
                    (90, 'ET-045', 1, 'Clutch engagement where fitted'),
                    (91, 'ET-046', 1, 'Clutch hydraulic leakage where fitted'),
                    (92, 'ET-047', 1, 'Transmission diagnostic fault evidence'),
                    (93, 'ET-048', 1, 'Differential leakage or abnormal noise'),
                    (94, 'ET-049', 1, 'Driveline vibration under controlled assessment'),
                    (95, 'ET-050', 1, 'Engine or transmission abnormality during controlled assessment'),
                    (96, 'EP-001', 2, 'Bonnet outer panel condition'),
                    (97, 'EP-002', 2, 'Bonnet paint finish consistency'),
                    (98, 'EP-003', 2, 'Bonnet hinges, latch and secondary catch'),
                    (99, 'EP-004', 2, 'Front bumper condition and attachment'),
                    (100, 'EP-005', 2, 'Rear bumper condition and attachment'),
                    (101, 'EP-006', 2, 'Front grille condition'),
                    (102, 'EP-007', 2, 'Left front wing condition and paint'),
                    (103, 'EP-008', 2, 'Right front wing condition and paint'),
                    (104, 'EP-009', 2, 'Left front door skin and paint'),
                    (105, 'EP-010', 2, 'Right front door skin and paint'),
                    (106, 'EP-011', 2, 'Left rear door skin and paint'),
                    (107, 'EP-012', 2, 'Right rear door skin and paint'),
                    (108, 'EP-013', 2, 'Left rear quarter finish'),
                    (109, 'EP-014', 2, 'Right rear quarter finish'),
                    (110, 'EP-015', 2, 'Roof outer panel and paint'),
                    (111, 'EP-016', 2, 'Boot lid or tailgate outer panel and paint'),
                    (112, 'EP-017', 2, 'Boot lid or tailgate hinges and latch'),
                    (113, 'EP-018', 2, 'Front left door hinge and latch operation'),
                    (114, 'EP-019', 2, 'Front right door hinge and latch operation'),
                    (115, 'EP-020', 2, 'Rear left door hinge and latch operation'),
                    (116, 'EP-021', 2, 'Rear right door hinge and latch operation'),
                    (117, 'EP-022', 2, 'Door aperture weather seals'),
                    (118, 'EP-023', 2, 'Boot or tailgate weather seal'),
                    (119, 'EP-024', 2, 'Windscreen condition'),
                    (120, 'EP-025', 2, 'Rear glass condition'),
                    (121, 'EP-026', 2, 'Left side glazing condition'),
                    (122, 'EP-027', 2, 'Right side glazing condition'),
                    (123, 'EP-028', 2, 'Left exterior mirror housing and glass'),
                    (124, 'EP-029', 2, 'Right exterior mirror housing and glass'),
                    (125, 'EP-030', 2, 'Left headlamp lens and housing'),
                    (126, 'EP-031', 2, 'Right headlamp lens and housing'),
                    (127, 'EP-032', 2, 'Left rear lamp lens and housing'),
                    (128, 'EP-033', 2, 'Right rear lamp lens and housing'),
                    (129, 'EP-034', 2, 'Front fog or auxiliary lamp housing where fitted'),
                    (130, 'EP-035', 2, 'Exterior handles and trim attachment'),
                    (131, 'EP-036', 2, 'Wheel-arch liners and splash guards'),
                    (132, 'EP-037', 2, 'Exterior corrosion outside structural members'),
                    (133, 'EP-038', 2, 'Paint overspray evidence'),
                    (134, 'EP-039', 2, 'Paint thickness consistency across comparable panels'),
                    (135, 'EP-040', 2, 'Panel gap and flushness consistency'),
                    (136, 'EP-041', 2, 'Exterior repair filler evidence'),
                    (137, 'EP-042', 2, 'Sunroof exterior seal and drainage where fitted'),
                    (138, 'EP-043', 2, 'Aerial and roof accessory attachment'),
                    (139, 'EP-044', 2, 'Number plate identity, condition and attachment'),
                    (140, 'EP-045', 2, 'External water ingress evidence at openings'),
                    (141, 'IE-001', 3, 'Odometer display function and legibility'),
                    (142, 'IE-002', 3, 'Mileage consistency with available retained records'),
                    (143, 'IE-003', 3, 'Mileage tampering evidence across available indicators'),
                    (144, 'IE-004', 3, 'Instrument warning-lamp self-check'),
                    (145, 'IE-005', 3, 'Instrument gauges and display operation'),
                    (146, 'IE-006', 3, 'Airbag warning and diagnostic evidence'),
                    (147, 'IE-007', 3, 'Visible airbag trim or deployment evidence'),
                    (148, 'IE-008', 3, 'Front safety-belt webbing, buckles and retractors'),
                    (149, 'IE-009', 3, 'Rear safety-belt webbing, buckles and retractors'),
                    (150, 'IE-010', 3, 'Front seat structure and adjustment'),
                    (151, 'IE-011', 3, 'Rear seat structure and folding latches'),
                    (152, 'IE-012', 3, 'Seat upholstery condition'),
                    (153, 'IE-013', 3, 'Dashboard and interior trim condition'),
                    (154, 'IE-014', 3, 'Headlining condition'),
                    (155, 'IE-015', 3, 'Carpet and underlay condition'),
                    (156, 'IE-016', 3, 'Interior dampness or contamination evidence'),
                    (157, 'IE-017', 3, 'Air-conditioning cooling performance'),
                    (158, 'IE-018', 3, 'Blower speed and vent distribution'),
                    (159, 'IE-019', 3, 'Climate controls and demisting operation'),
                    (160, 'IE-020', 3, 'Cabin filter housing condition'),
                    (161, 'IE-021', 3, 'Front left window operation'),
                    (162, 'IE-022', 3, 'Front right window operation'),
                    (163, 'IE-023', 3, 'Rear left window operation'),
                    (164, 'IE-024', 3, 'Rear right window operation'),
                    (165, 'IE-025', 3, 'Central locking and supplied key operation'),
                    (166, 'IE-026', 3, 'Interior door release and child-lock operation'),
                    (167, 'IE-027', 3, 'Exterior mirror adjustment and folding where fitted'),
                    (168, 'IE-028', 3, 'Windscreen wipers and washers'),
                    (169, 'IE-029', 3, 'Rear wiper or washer where fitted'),
                    (170, 'IE-030', 3, 'Headlamp and high-beam operation'),
                    (171, 'IE-031', 3, 'Indicator and hazard-lamp operation'),
                    (172, 'IE-032', 3, 'Brake, tail and number-plate lamp operation'),
                    (173, 'IE-033', 3, 'Reverse and fog-lamp operation where fitted'),
                    (174, 'IE-034', 3, 'Horn operation'),
                    (175, 'IE-035', 3, 'Cabin and luggage lighting'),
                    (176, 'IE-036', 3, 'Audio, infotainment and connectivity operation'),
                    (177, 'IE-037', 3, 'Parking sensors and reversing camera where fitted'),
                    (178, 'IE-038', 3, 'Accessory power outlets and visible wiring alterations'),
                    (179, 'IE-039', 3, 'Battery condition, terminals and secure mounting'),
                    (180, 'IE-040', 3, 'Electric sunroof or powered roof operation where fitted'),
                    (181, 'UB-001', 4, 'Front left tyre tread and sidewall condition'),
                    (182, 'UB-002', 4, 'Front right tyre tread and sidewall condition'),
                    (183, 'UB-003', 4, 'Rear left tyre tread and sidewall condition'),
                    (184, 'UB-004', 4, 'Rear right tyre tread and sidewall condition'),
                    (185, 'UB-005', 4, 'Tyre size, load suitability and axle pairing'),
                    (186, 'UB-006', 4, 'Spare tyre or supplied mobility kit condition'),
                    (187, 'UB-007', 4, 'Wheel rim condition'),
                    (188, 'UB-008', 4, 'Wheel fastener condition and completeness'),
                    (189, 'UB-009', 4, 'Front brake friction material condition'),
                    (190, 'UB-010', 4, 'Rear brake friction material condition'),
                    (191, 'UB-011', 4, 'Front brake disc or drum condition'),
                    (192, 'UB-012', 4, 'Rear brake disc or drum condition'),
                    (193, 'UB-013', 4, 'Brake hose and pipe condition'),
                    (194, 'UB-014', 4, 'Brake hydraulic leakage'),
                    (195, 'UB-015', 4, 'Brake fluid level and visible condition'),
                    (196, 'UB-016', 4, 'Brake pedal and assistance operation'),
                    (197, 'UB-017', 4, 'Parking brake operation'),
                    (198, 'UB-018', 4, 'ABS warning and diagnostic evidence'),
                    (199, 'UB-019', 4, 'Front suspension spring condition'),
                    (200, 'UB-020', 4, 'Rear suspension spring condition'),
                    (201, 'UB-021', 4, 'Front damper condition and leakage'),
                    (202, 'UB-022', 4, 'Rear damper condition and leakage'),
                    (203, 'UB-023', 4, 'Suspension arm and bush condition'),
                    (204, 'UB-024', 4, 'Ball joint and stabilizer link condition'),
                    (205, 'UB-025', 4, 'Steering rack, joint and boot condition'),
                    (206, 'UB-026', 4, 'Steering assistance leakage or abnormality'),
                    (207, 'UB-027', 4, 'Wheel bearing play or abnormal noise'),
                    (208, 'UB-028', 4, 'Driveshaft and CV boot condition'),
                    (209, 'UB-029', 4, 'Underbody shields and exposed line attachment'),
                    (210, 'UB-030', 4, 'Directional stability, braking pull or suspension noise during controlled assessment')
                    ) catalog(ordinal, code, category, description);
                $$;

                CREATE OR REPLACE FUNCTION guard_inspection_draft() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        RAISE EXCEPTION 'Inspection draft provenance is retained'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionDrafts_Immutable';
                    END IF;
                    IF TG_OP = 'UPDATE' THEN
                        IF NOT OLD."IsSigned" AND NEW."IsSigned" AND
                           (to_jsonb(NEW) - 'IsSigned') = (to_jsonb(OLD) - 'IsSigned') THEN
                            RETURN NEW;
                        END IF;
                        IF (NEW."Id", NEW."VehicleId", NEW."BranchId", NEW."CreatedAt", NEW."Revision") IS DISTINCT FROM
                           (OLD."Id", OLD."VehicleId", OLD."BranchId", OLD."CreatedAt", OLD."Revision") OR
                           NEW."AssignmentVersion" - 1 <> OLD."AssignmentVersion" OR
                           NEW."TechnicianId" = OLD."TechnicianId" OR OLD."IsSigned" OR NEW."IsSigned" THEN
                            RAISE EXCEPTION 'Only an unsigned consecutive recorded reassignment may change a draft'
                                USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionDrafts_Immutable';
                        END IF;
                    ELSIF NEW."AssignmentVersion" <> 1 OR NEW."Revision" <> 1 OR NEW."IsSigned" THEN
                        RAISE EXCEPTION 'Initial claim requires unsigned revision one'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionDrafts_Version';
                    END IF;
                    PERFORM 1 FROM "Vehicles" WHERE "Id" = NEW."VehicleId" AND "BranchId" = NEW."BranchId"
                        AND "Status" = 'PendingInspection' FOR UPDATE;
                    IF NOT FOUND THEN
                        RAISE EXCEPTION 'Inspection requires a matching pending vehicle'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionDrafts_Vehicle';
                    END IF;
                    RETURN NEW;
                END $$;

                CREATE FUNCTION require_inspection_seal() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "InspectionDrafts" d WHERE d."Id" = NEW."Id" AND
                        d."IsSigned" IS DISTINCT FROM EXISTS (SELECT 1 FROM "InspectionSignedRevisions" r
                            WHERE r."InspectionId" = d."Id" AND r."Revision" = d."Revision" AND
                                r."TechnicianId" = d."TechnicianId" AND r."AssignmentVersion" = d."AssignmentVersion")) THEN
                        RAISE EXCEPTION 'The draft seal requires its exact retained signed revision'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionDrafts_Seal';
                    END IF;
                    RETURN NULL;
                END $$;
                CREATE CONSTRAINT TRIGGER inspection_draft_seal AFTER INSERT OR UPDATE ON "InspectionDrafts"
                    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION require_inspection_seal();

                CREATE FUNCTION fence_unsigned_inspection_write() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE
                    sealed boolean;
                BEGIN
                    IF TG_TABLE_NAME = 'InspectionPhotos' AND TG_OP = 'UPDATE' THEN
                        -- Attachment and cleanup use unchanged photo updates to fence provider races independently.
                        IF NEW."AvailableAt" IS NOT DISTINCT FROM OLD."AvailableAt" THEN
                            RETURN NEW;
                        END IF;
                    END IF;
                    -- Sign-off changes this row, so existing row locks also reject older repeatable-read snapshots.
                    SELECT "IsSigned" INTO sealed FROM "InspectionDrafts" WHERE "Id" = NEW."InspectionId" FOR UPDATE;
                    IF sealed IS DISTINCT FROM false THEN
                        RAISE EXCEPTION 'Signed inspections require an authorized new revision'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionDrafts_Signed';
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER inspection_unsigned_write_guard BEFORE INSERT ON "InspectionDraftSaves"
                    FOR EACH ROW EXECUTE FUNCTION fence_unsigned_inspection_write();
                CREATE TRIGGER inspection_unsigned_write_guard BEFORE INSERT OR UPDATE ON "InspectionPhotos"
                    FOR EACH ROW EXECUTE FUNCTION fence_unsigned_inspection_write();

                CREATE FUNCTION guard_inspection_signed_revision() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE
                    actor "AspNetUsers"%ROWTYPE;
                    draft "InspectionDrafts"%ROWTYPE;
                    vehicle "Vehicles"%ROWTYPE;
                    saved "InspectionDraftSaves"%ROWTYPE;
                    vehicle_id uuid;
                    checklist jsonb;
                    assessment jsonb;
                    total numeric;
                    severity integer;
                    grade integer;
                    rejected boolean;
                    expected jsonb;
                BEGIN
                    IF TG_OP <> 'INSERT' THEN
                        RAISE EXCEPTION 'Signed inspection revisions are immutable'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionSignedRevisions_Immutable';
                    END IF;
                    SELECT * INTO actor FROM "AspNetUsers" WHERE "Id" = NEW."TechnicianId" FOR SHARE;
                    PERFORM 1 FROM "Branches" WHERE "Id" = NEW."BranchId" AND "IsActive" FOR SHARE;
                    IF NOT FOUND OR actor."Id" IS NULL OR NOT actor."IsActive" OR
                       actor."Role" <> 'WorkshopTechnician' OR actor."BranchId" IS DISTINCT FROM NEW."BranchId" THEN
                        RAISE EXCEPTION 'Signing requires a current active branch technician'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionSignedRevisions_Eligibility';
                    END IF;
                    SELECT "VehicleId" INTO vehicle_id FROM "InspectionDrafts" WHERE "Id" = NEW."InspectionId";
                    SELECT * INTO vehicle FROM "Vehicles" WHERE "Id" = vehicle_id FOR UPDATE;
                    SELECT * INTO draft FROM "InspectionDrafts" WHERE "Id" = NEW."InspectionId" FOR UPDATE;
                    SELECT * INTO saved FROM "InspectionDraftSaves" WHERE "InspectionId" = NEW."InspectionId"
                        ORDER BY "Version" DESC LIMIT 1;
                    IF draft."Id" IS NULL OR draft."IsSigned" OR draft."BranchId" <> NEW."BranchId" OR
                       draft."TechnicianId" <> NEW."TechnicianId" OR draft."AssignmentVersion" <> NEW."AssignmentVersion" OR
                       draft."Revision" <> NEW."Revision" OR vehicle."Status" <> 'PendingInspection' OR
                       saved."Id" IS DISTINCT FROM NEW."SaveId" OR saved."Version" <> NEW."SaveVersion" OR
                       saved."ActorId" <> NEW."TechnicianId" OR saved."AssignmentVersion" <> NEW."AssignmentVersion" OR
                       saved."ExaminedPointCodesJson" IS NULL OR NEW."SignedAt" < saved."RecordedAt" OR
                       NEW."SignedAt" > clock_timestamp() THEN
                        RAISE EXCEPTION 'Sign-off must select the latest current assigned attested save'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionSignedRevisions_Save';
                    END IF;

                    SELECT coalesce(sum((entry->>'estimatedRepairCost')::numeric), 0),
                           coalesce(max((entry->>'severity')::integer), 0) INTO total, severity
                    FROM jsonb_array_elements(saved."EntriesJson") entry WHERE entry->>'kind' = '0';
                    rejected := total > 15000 OR severity = 3;
                    grade := CASE WHEN rejected THEN NULL WHEN total >= 2500 OR severity = 2 THEN 2
                        WHEN total >= 500 OR severity = 1 THEN 1 ELSE 0 END;
                    assessment := jsonb_build_object('estimatedRepairCost', total, 'grade', grade, 'isRejected', rejected);

                    SELECT jsonb_agg(point.value || jsonb_build_object(
                        'kind', entry->'kind', 'severity', entry->'severity',
                        'estimatedRepairCost', coalesce(entry->'estimatedRepairCost', '0'::jsonb), 'notes', entry->'notes',
                        'photos', coalesce((SELECT jsonb_agg(jsonb_build_object(
                            'id', photo."Id", 'contentType', photo."ContentType", 'bytes', photo."Bytes",
                            'sha256', photo."Sha256", 'width', photo."Width", 'height', photo."Height") ORDER BY photo."Id")
                            FROM "InspectionDraftPhotos" link JOIN "InspectionPhotos" photo ON photo."Id" = link."PhotoId"
                            WHERE link."SaveId" = saved."Id" AND link."PointCode" = point.value->>'code'), '[]'::jsonb))
                        ORDER BY point.ordinal) INTO checklist
                    FROM jsonb_array_elements(inspection_catalog_v1()) WITH ORDINALITY point(value, ordinal)
                    LEFT JOIN LATERAL (SELECT item AS entry FROM jsonb_array_elements(saved."EntriesJson") item
                        WHERE item->>'pointCode' = point.value->>'code') finding ON true;

                    expected := jsonb_build_object(
                        'operationId', NEW."Id", 'inspectionId', NEW."InspectionId", 'revision', NEW."Revision",
                        'branchId', NEW."BranchId", 'saveId', NEW."SaveId", 'saveVersion', NEW."SaveVersion",
                        'assignmentVersion', NEW."AssignmentVersion", 'technicianId', NEW."TechnicianId",
                        'technicianIdentity', actor."UserName", 'catalogVersion', saved."CatalogVersion",
                        'assessment', assessment, 'checklist', checklist, 'signedAt', NEW."ReportJson"->'signedAt',
                        'vehicle', jsonb_build_object('id', vehicle."Id", 'intakeAppointmentId', vehicle."IntakeAppointmentId",
                            'intakeOwnerId', vehicle."IntakeOwnerId", 'vin', vehicle."Vin", 'registrationPlate', vehicle."RegistrationPlate",
                            'make', vehicle."Make", 'model', vehicle."Model", 'trim', vehicle."Trim",
                            'manufactureYear', vehicle."ManufactureYear", 'mileageKilometres', vehicle."MileageKilometres",
                            'checkedInAt', NEW."ReportJson"->'vehicle'->'checkedInAt'));
                    IF NEW."ReportJson" IS DISTINCT FROM expected OR
                       (NEW."ReportJson"->>'signedAt')::timestamptz IS DISTINCT FROM NEW."SignedAt" OR
                       (NEW."ReportJson"->'vehicle'->>'checkedInAt')::timestamptz IS DISTINCT FROM vehicle."CreatedAt" THEN
                        RAISE EXCEPTION 'Signed report values must freeze their exact original sources'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionSignedRevisions_Report';
                    END IF;
                    UPDATE "InspectionDrafts" SET "IsSigned" = true WHERE "Id" = draft."Id";
                    RETURN NEW;
                END $$;
                CREATE TRIGGER inspection_signed_revision_guard BEFORE INSERT OR UPDATE OR DELETE ON "InspectionSignedRevisions"
                    FOR EACH ROW EXECUTE FUNCTION guard_inspection_signed_revision();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER inspection_unsigned_write_guard ON "InspectionDraftSaves";
                DROP TRIGGER inspection_unsigned_write_guard ON "InspectionPhotos";
                DROP TRIGGER inspection_draft_seal ON "InspectionDrafts";
                DROP FUNCTION fence_unsigned_inspection_write();
                DROP FUNCTION require_inspection_seal();
                CREATE OR REPLACE FUNCTION guard_inspection_draft() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        RAISE EXCEPTION 'Inspection draft provenance is retained'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionDrafts_Immutable';
                    END IF;
                    IF TG_OP = 'UPDATE' THEN
                        IF (NEW."Id", NEW."VehicleId", NEW."BranchId", NEW."CreatedAt") IS DISTINCT FROM
                           (OLD."Id", OLD."VehicleId", OLD."BranchId", OLD."CreatedAt") OR
                           NEW."AssignmentVersion" - 1 <> OLD."AssignmentVersion" OR
                           NEW."TechnicianId" = OLD."TechnicianId" THEN
                            RAISE EXCEPTION 'Only a consecutive recorded reassignment may change a draft'
                                USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionDrafts_Immutable';
                        END IF;
                    ELSIF NEW."AssignmentVersion" <> 1 THEN
                        RAISE EXCEPTION 'Initial claim requires version one'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionDrafts_Version';
                    END IF;
                    PERFORM 1 FROM "Vehicles" WHERE "Id" = NEW."VehicleId" AND "BranchId" = NEW."BranchId"
                        AND "Status" = 'PendingInspection' FOR UPDATE;
                    IF NOT FOUND THEN
                        RAISE EXCEPTION 'Inspection requires a matching pending vehicle'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_InspectionDrafts_Vehicle';
                    END IF;
                    RETURN NEW;
                END $$;
                """);
            migrationBuilder.DropTable(
                name: "InspectionSignedRevisions");

            migrationBuilder.Sql("""
                DROP FUNCTION guard_inspection_signed_revision();
                DROP FUNCTION inspection_catalog_v1();
                """);

            migrationBuilder.DropCheckConstraint(
                name: "CK_InspectionDrafts_Shape",
                table: "InspectionDrafts");

            migrationBuilder.DropColumn(
                name: "IsSigned",
                table: "InspectionDrafts");

            migrationBuilder.DropColumn(
                name: "Revision",
                table: "InspectionDrafts");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InspectionDrafts_Shape",
                table: "InspectionDrafts",
                sql: "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"AssignmentVersion\" > 0");
        }
    }
}

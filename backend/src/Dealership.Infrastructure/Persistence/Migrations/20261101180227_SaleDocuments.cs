using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dealership.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SaleDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "SalesOrders" WHERE "Status" = 'Settled') THEN
                        RAISE EXCEPTION 'Existing settlements require reviewed document reconciliation before rollout';
                    END IF;
                END $$;
                """);
            migrationBuilder.AddColumn<string>(
                name: "SettlementReview",
                table: "SalesOrderEvents",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_SalesOrderEvents_Id_SalesOrderId",
                table: "SalesOrderEvents",
                columns: ["Id", "SalesOrderId"]);

            migrationBuilder.CreateTable(
                name: "ReceiptNumberCounters",
                columns: table => new
                {
                    Year = table.Column<int>(type: "integer", nullable: false),
                    LastValue = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceiptNumberCounters", x => x.Year);
                    table.CheckConstraint("CK_ReceiptNumberCounters_Shape", "\"Year\" BETWEEN 2000 AND 9999 AND \"LastValue\" BETWEEN 0 AND 999999");
                });

            migrationBuilder.CreateTable(
                name: "SaleDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SalesOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    SettlementOperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Number = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false, collation: "C"),
                    IssuedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ContentsJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaleDocuments", x => x.Id);
                    table.CheckConstraint("CK_SaleDocuments_Shape", "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND isfinite(\"IssuedAt\") AND ((\"Kind\" = 'PurchaseOrder' AND \"Number\" ~ '^VPO-[0-9]{4}-[0-9]{6}$') OR (\"Kind\" = 'PaymentReceipt' AND \"Number\" ~ '^VRC-[0-9]{4}-[0-9]{6}$')) AND jsonb_typeof(\"ContentsJson\") = 'object' AND octet_length(\"ContentsJson\"::text) <= 65536");
                    table.ForeignKey(
                        name: "FK_SaleDocuments_SalesOrderEvents_SettlementOperationId_SalesO~",
                        columns: x => new { x.SettlementOperationId, x.SalesOrderId },
                        principalTable: "SalesOrderEvents",
                        principalColumns: ["Id", "SalesOrderId"],
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SaleDocuments_SalesOrders_SalesOrderId",
                        column: x => x.SalesOrderId,
                        principalTable: "SalesOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SaleDocuments_Number",
                table: "SaleDocuments",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SaleDocuments_SalesOrderId_Kind",
                table: "SaleDocuments",
                columns: ["SalesOrderId", "Kind"],
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SaleDocuments_SettlementOperationId_SalesOrderId",
                table: "SaleDocuments",
                columns: ["SettlementOperationId", "SalesOrderId"]);
            migrationBuilder.Sql("""
                CREATE FUNCTION guard_settlement_review() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE
                    sale "SalesOrders"%ROWTYPE;
                    profile "CustomerProfiles"%ROWTYPE;
                    vehicle "Vehicles"%ROWTYPE;
                    reviewed jsonb;
                BEGIN
                    IF NEW."Kind" <> 'Settled' THEN
                        IF NEW."SettlementReview" IS NOT NULL THEN
                            RAISE EXCEPTION 'Only settlement captures reviewed details'
                                USING ERRCODE = '23514', CONSTRAINT = 'CK_SalesOrderEvents_Review';
                        END IF;
                        RETURN NEW;
                    END IF;
                    SELECT * INTO sale FROM "SalesOrders" WHERE "Id" = NEW."SalesOrderId" FOR UPDATE;
                    SELECT * INTO profile FROM "CustomerProfiles" WHERE "UserId" = sale."CustomerId" FOR SHARE;
                    SELECT * INTO vehicle FROM "Vehicles" WHERE "Id" = sale."VehicleId" FOR SHARE;
                    reviewed := NEW."SettlementReview";
                    IF NOT COALESCE(jsonb_typeof(reviewed) = 'object' AND
                        reviewed->>'BuyerNric' = profile."Nric" AND
                        reviewed->>'BuyerNric' = (SELECT "BuyerNric" FROM "Reservations" WHERE "Id" = sale."Id") AND
                        jsonb_typeof(reviewed->'BuyerName') = 'string' AND
                        length(reviewed->>'BuyerName') BETWEEN 1 AND 200 AND
                        reviewed->>'BuyerName' = btrim(reviewed->>'BuyerName') AND
                        reviewed->>'BuyerName' ~ '[^[:space:]]' AND reviewed->>'BuyerName' !~ '[[:cntrl:]]' AND
                        jsonb_typeof(reviewed->'BuyerAddress') = 'string' AND
                        length(reviewed->>'BuyerAddress') BETWEEN 1 AND 1000 AND
                        reviewed->>'BuyerAddress' = btrim(reviewed->>'BuyerAddress') AND
                        reviewed->>'BuyerAddress' ~ '[^[:space:]]' AND reviewed->>'BuyerAddress' !~ '[[:cntrl:]]' AND
                        reviewed->'Vehicle' = jsonb_build_object('Id', vehicle."Id",
                            'RegistrationPlate', vehicle."RegistrationPlate", 'Vin', vehicle."Vin",
                            'Make', vehicle."Make", 'Model', vehicle."Model", 'Trim', vehicle."Trim",
                            'ManufactureYear', vehicle."ManufactureYear", 'MileageKilometres', vehicle."MileageKilometres") AND
                        sale."Status" = 'Settled' AND NEW."Sequence" = sale."Version" AND
                        NEW."ActorId" = sale."AgentId" AND NEW."RecordedAt" = sale."SettledAt", false) THEN
                        RAISE EXCEPTION 'Settlement requires reviewed buyer identity and exact vehicle facts'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_SalesOrderEvents_Review';
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE CONSTRAINT TRIGGER settlement_review_guard AFTER INSERT ON "SalesOrderEvents"
                    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION guard_settlement_review();

                CREATE FUNCTION guard_sale_document() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE
                    sale "SalesOrders"%ROWTYPE;
                    receipt "SalesOrderEvents"%ROWTYPE;
                    branch "Branches"%ROWTYPE;
                    reservation_number text;
                BEGIN
                    IF TG_OP <> 'INSERT' THEN
                        RAISE EXCEPTION 'Issued sales documents are immutable'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_SaleDocuments_Immutable';
                    END IF;
                    SELECT * INTO sale FROM "SalesOrders" WHERE "Id" = NEW."SalesOrderId" FOR UPDATE;
                    SELECT * INTO receipt FROM "SalesOrderEvents" WHERE "Id" = NEW."SettlementOperationId";
                    SELECT * INTO branch FROM "Branches" WHERE "Id" = sale."BranchId" FOR SHARE;
                    SELECT "Number" INTO reservation_number FROM "Reservations" WHERE "Id" = sale."Id";
                    IF NOT COALESCE(receipt."Kind" = 'Settled' AND receipt."SalesOrderId" = sale."Id" AND
                        sale."Status" = 'Settled' AND receipt."Sequence" = sale."Version" AND
                        NEW."IssuedAt" = receipt."RecordedAt" AND receipt."SettlementReview" IS NOT NULL AND
                        (NEW."Kind" <> 'PurchaseOrder' OR NEW."Number" = sale."OrderNumber") AND
                        NEW."ContentsJson" = jsonb_build_object('CustomerId', sale."CustomerId",
                            'BranchId', sale."BranchId", 'BranchName', branch."Name", 'BranchAddress', branch."Address",
                            'ReservationNumber', reservation_number, 'Review', receipt."SettlementReview",
                            'Sale', receipt."Snapshot"), false) THEN
                        RAISE EXCEPTION 'Documents must retain the exact settlement and reviewed facts'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_SaleDocuments_Settlement';
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER sale_document_guard BEFORE UPDATE OR DELETE ON "SaleDocuments"
                    FOR EACH ROW EXECUTE FUNCTION guard_sale_document();
                CREATE CONSTRAINT TRIGGER sale_document_capture AFTER INSERT ON "SaleDocuments"
                    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION guard_sale_document();

                CREATE FUNCTION require_settlement_documents() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF NEW."Status" = 'Settled' AND OLD."Status" <> 'Settled' AND
                        (SELECT count(*) FROM "SaleDocuments" d JOIN "SalesOrderEvents" e
                            ON e."Id" = d."SettlementOperationId" AND e."SalesOrderId" = d."SalesOrderId"
                            WHERE d."SalesOrderId" = NEW."Id" AND e."Kind" = 'Settled' AND
                                e."Sequence" = NEW."Version" AND d."IssuedAt" = NEW."SettledAt") <> 2 THEN
                        RAISE EXCEPTION 'Settlement requires both issued documents in the same transaction'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_SalesOrders_Documents';
                    END IF;
                    RETURN NULL;
                END $$;
                CREATE CONSTRAINT TRIGGER settlement_documents_required AFTER UPDATE ON "SalesOrders"
                    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION require_settlement_documents();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "SaleDocuments") THEN
                        RAISE EXCEPTION 'Cannot remove issued sales documents';
                    END IF;
                END $$;
                DROP TRIGGER settlement_documents_required ON "SalesOrders";
                DROP FUNCTION require_settlement_documents();
                DROP TRIGGER sale_document_guard ON "SaleDocuments";
                DROP TRIGGER sale_document_capture ON "SaleDocuments";
                DROP FUNCTION guard_sale_document();
                DROP TRIGGER settlement_review_guard ON "SalesOrderEvents";
                DROP FUNCTION guard_settlement_review();
                """);
            migrationBuilder.DropTable(
                name: "ReceiptNumberCounters");

            migrationBuilder.DropTable(
                name: "SaleDocuments");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_SalesOrderEvents_Id_SalesOrderId",
                table: "SalesOrderEvents");

            migrationBuilder.DropColumn(
                name: "SettlementReview",
                table: "SalesOrderEvents");
        }
    }
}

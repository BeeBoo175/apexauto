using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dealership.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SaleDocumentCorrections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SaleDocuments_SalesOrderId_Kind",
                table: "SaleDocuments");

            migrationBuilder.AddColumn<Guid>(
                name: "CorrectionOperationId",
                table: "SaleDocuments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_SaleDocuments_Id_SalesOrderId",
                table: "SaleDocuments",
                columns: ["Id", "SalesOrderId"]);

            migrationBuilder.CreateTable(
                name: "SaleDocumentCorrections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SalesOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalDocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpectedVersion = table.Column<long>(type: "bigint", nullable: false),
                    BuyerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BuyerAddress = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaleDocumentCorrections", x => x.Id);
                    table.UniqueConstraint("AK_SaleDocumentCorrections_Id_SalesOrderId", x => new { x.Id, x.SalesOrderId });
                    table.CheckConstraint("CK_SaleDocumentCorrections_Shape", "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"ExpectedVersion\" > 0 AND isfinite(\"RecordedAt\") AND length(\"BuyerName\") BETWEEN 1 AND 200 AND length(\"BuyerAddress\") BETWEEN 1 AND 1000 AND length(\"Reason\") BETWEEN 1 AND 1000 AND \"BuyerName\" = btrim(\"BuyerName\") AND \"BuyerName\" ~ '[^[:space:]]' AND \"BuyerName\" !~ '[[:cntrl:]]' AND \"BuyerAddress\" = btrim(\"BuyerAddress\") AND \"BuyerAddress\" ~ '[^[:space:]]' AND \"BuyerAddress\" !~ '[[:cntrl:]]' AND \"Reason\" = btrim(\"Reason\") AND \"Reason\" ~ '[^[:space:]]' AND \"Reason\" !~ '[[:cntrl:]]'");
                    table.ForeignKey(
                        name: "FK_SaleDocumentCorrections_AspNetUsers_ActorId",
                        column: x => x.ActorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SaleDocumentCorrections_SaleDocuments_OriginalDocumentId_Sa~",
                        columns: x => new { x.OriginalDocumentId, x.SalesOrderId },
                        principalTable: "SaleDocuments",
                        principalColumns: ["Id", "SalesOrderId"],
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SaleDocumentCorrections_SalesOrders_SalesOrderId",
                        column: x => x.SalesOrderId,
                        principalTable: "SalesOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SaleDocuments_CorrectionOperationId",
                table: "SaleDocuments",
                column: "CorrectionOperationId",
                unique: true,
                filter: "\"CorrectionOperationId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SaleDocuments_CorrectionOperationId_SalesOrderId",
                table: "SaleDocuments",
                columns: ["CorrectionOperationId", "SalesOrderId"]);

            migrationBuilder.CreateIndex(
                name: "IX_SaleDocuments_SalesOrderId_Kind",
                table: "SaleDocuments",
                columns: ["SalesOrderId", "Kind"],
                unique: true,
                filter: "\"CorrectionOperationId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SaleDocumentCorrections_ActorId",
                table: "SaleDocumentCorrections",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleDocumentCorrections_OriginalDocumentId",
                table: "SaleDocumentCorrections",
                column: "OriginalDocumentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SaleDocumentCorrections_OriginalDocumentId_SalesOrderId",
                table: "SaleDocumentCorrections",
                columns: ["OriginalDocumentId", "SalesOrderId"]);

            migrationBuilder.CreateIndex(
                name: "IX_SaleDocumentCorrections_SalesOrderId",
                table: "SaleDocumentCorrections",
                column: "SalesOrderId");

            migrationBuilder.AddForeignKey(
                name: "FK_SaleDocuments_SaleDocumentCorrections_CorrectionOperationId~",
                table: "SaleDocuments",
                columns: ["CorrectionOperationId", "SalesOrderId"],
                principalTable: "SaleDocumentCorrections",
                principalColumns: ["Id", "SalesOrderId"],
                onDelete: ReferentialAction.Restrict);
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION guard_sale_document() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE
                    sale "SalesOrders"%ROWTYPE;
                    receipt "SalesOrderEvents"%ROWTYPE;
                    branch "Branches"%ROWTYPE;
                    reservation_number text;
                    correction "SaleDocumentCorrections"%ROWTYPE;
                    original "SaleDocuments"%ROWTYPE;
                BEGIN
                    IF TG_OP <> 'INSERT' THEN
                        RAISE EXCEPTION 'Issued sales documents are immutable'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_SaleDocuments_Immutable';
                    END IF;
                    IF NEW."CorrectionOperationId" IS NOT NULL THEN
                        SELECT * INTO correction FROM "SaleDocumentCorrections" WHERE "Id" = NEW."CorrectionOperationId";
                        SELECT * INTO original FROM "SaleDocuments" WHERE "Id" = correction."OriginalDocumentId";
                        IF NOT COALESCE(correction."SalesOrderId" = NEW."SalesOrderId" AND
                            original."SalesOrderId" = NEW."SalesOrderId" AND original."Id" <> NEW."Id" AND
                            original."Kind" = NEW."Kind" AND original."IssuingOperationId" = NEW."IssuingOperationId" AND
                            NEW."IssuedAt" = correction."RecordedAt" AND NEW."IssuedAt" >= original."IssuedAt" AND
                            NEW."ContentsJson" = jsonb_set(jsonb_set(original."ContentsJson",
                                '{Review,BuyerName}', to_jsonb(correction."BuyerName")),
                                '{Review,BuyerAddress}', to_jsonb(correction."BuyerAddress")) AND
                            NEW."ContentsJson" IS DISTINCT FROM original."ContentsJson", false) THEN
                            RAISE EXCEPTION 'Replacement documents may only correct buyer name or address'
                                USING ERRCODE = '23514', CONSTRAINT = 'CK_SaleDocuments_Correction';
                        END IF;
                        RETURN NEW;
                    END IF;
                    SELECT * INTO sale FROM "SalesOrders" WHERE "Id" = NEW."SalesOrderId" FOR UPDATE;
                    SELECT * INTO receipt FROM "SalesOrderEvents" WHERE "Id" = NEW."IssuingOperationId";
                    IF NEW."Kind" = 'HandoverNote' THEN
                        IF NOT COALESCE(receipt."Kind" = 'Delivered' AND receipt."SalesOrderId" = sale."Id" AND
                            sale."Status" = 'Delivered' AND receipt."Sequence" = sale."Version" AND
                            NEW."IssuedAt" = receipt."RecordedAt" AND
                            NEW."ContentsJson"->'Sale' = receipt."Snapshot" AND
                            NEW."ContentsJson" - 'Sale' = (SELECT "ContentsJson" - 'Sale' FROM "SaleDocuments"
                                WHERE "SalesOrderId" = sale."Id" AND "Kind" = 'PurchaseOrder' AND "CorrectionOperationId" IS NULL), false) THEN
                            RAISE EXCEPTION 'Handover notes retain settlement identity and exact delivery evidence'
                                USING ERRCODE = '23514', CONSTRAINT = 'CK_SaleDocuments_Handover';
                        END IF;
                        RETURN NEW;
                    END IF;
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
                CREATE OR REPLACE FUNCTION require_settlement_documents() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF NEW."Status" = 'Settled' AND OLD."Status" <> 'Settled' AND
                        (SELECT count(*) FROM "SaleDocuments" d JOIN "SalesOrderEvents" e
                            ON e."Id" = d."IssuingOperationId" AND e."SalesOrderId" = d."SalesOrderId"
                            WHERE d."SalesOrderId" = NEW."Id" AND d."CorrectionOperationId" IS NULL AND e."Kind" = 'Settled' AND
                                e."Sequence" = NEW."Version" AND d."IssuedAt" = NEW."SettledAt") <> 2 THEN
                        RAISE EXCEPTION 'Settlement requires both issued documents in the same transaction'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_SalesOrders_Documents';
                    END IF;
                    RETURN NULL;
                END $$;
                CREATE FUNCTION guard_sale_document_correction() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE
                    actor "AspNetUsers"%ROWTYPE;
                    sale "SalesOrders"%ROWTYPE;
                    original "SaleDocuments"%ROWTYPE;
                BEGIN
                    IF TG_OP <> 'INSERT' THEN
                        RAISE EXCEPTION 'Document correction evidence is append-only'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_SaleDocumentCorrections_Immutable';
                    END IF;
                    SELECT * INTO actor FROM "AspNetUsers" WHERE "Id" = NEW."ActorId" FOR SHARE;
                    SELECT * INTO sale FROM "SalesOrders" WHERE "Id" = NEW."SalesOrderId" FOR UPDATE;
                    SELECT * INTO original FROM "SaleDocuments" WHERE "Id" = NEW."OriginalDocumentId";
                    PERFORM 1 FROM "Branches" WHERE "Id" = sale."BranchId" AND "IsActive" FOR SHARE;
                    IF NOT FOUND OR NOT COALESCE(actor."IsActive" AND
                        (actor."Role" = 'GeneralManager' OR
                            (actor."Role" = 'BranchManager' AND actor."BranchId" = sale."BranchId")) AND
                        sale."Status" IN ('Settled', 'Delivered') AND NEW."ExpectedVersion" = sale."Version" AND
                        original."SalesOrderId" = sale."Id" AND NEW."RecordedAt" >= original."IssuedAt" AND
                        NEW."RecordedAt" <= clock_timestamp() AND
                        (NEW."BuyerName" IS DISTINCT FROM original."ContentsJson"->'Review'->>'BuyerName' OR
                         NEW."BuyerAddress" IS DISTINCT FROM original."ContentsJson"->'Review'->>'BuyerAddress') AND
                        NOT EXISTS (SELECT 1 FROM "SalesOrderEvents" WHERE "Id" = NEW."Id"), false) THEN
                        RAISE EXCEPTION 'Correction requires current manager scope, version and changed buyer spelling'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_SaleDocumentCorrections_Scope';
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER sale_document_correction_guard BEFORE INSERT OR UPDATE OR DELETE ON "SaleDocumentCorrections"
                    FOR EACH ROW EXECUTE FUNCTION guard_sale_document_correction();

                CREATE FUNCTION require_sale_document_replacement() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM "SaleDocuments" WHERE "CorrectionOperationId" = NEW."Id" AND
                        "SalesOrderId" = NEW."SalesOrderId") THEN
                        RAISE EXCEPTION 'Void and replacement must commit together'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_SaleDocumentCorrections_Replacement';
                    END IF;
                    RETURN NULL;
                END $$;
                CREATE CONSTRAINT TRIGGER sale_document_replacement_required AFTER INSERT ON "SaleDocumentCorrections"
                    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION require_sale_document_replacement();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "SaleDocumentCorrections") THEN
                        RAISE EXCEPTION 'Cannot remove retained document corrections';
                    END IF;
                END $$;
                DROP TRIGGER sale_document_replacement_required ON "SaleDocumentCorrections";
                DROP FUNCTION require_sale_document_replacement();
                DROP TRIGGER sale_document_correction_guard ON "SaleDocumentCorrections";
                DROP FUNCTION guard_sale_document_correction();
                CREATE OR REPLACE FUNCTION guard_sale_document() RETURNS trigger LANGUAGE plpgsql AS $$
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
                    SELECT * INTO receipt FROM "SalesOrderEvents" WHERE "Id" = NEW."IssuingOperationId";
                    IF NEW."Kind" = 'HandoverNote' THEN
                        IF NOT COALESCE(receipt."Kind" = 'Delivered' AND receipt."SalesOrderId" = sale."Id" AND
                            sale."Status" = 'Delivered' AND receipt."Sequence" = sale."Version" AND
                            NEW."IssuedAt" = receipt."RecordedAt" AND
                            NEW."ContentsJson"->'Sale' = receipt."Snapshot" AND
                            NEW."ContentsJson" - 'Sale' = (SELECT "ContentsJson" - 'Sale' FROM "SaleDocuments"
                                WHERE "SalesOrderId" = sale."Id" AND "Kind" = 'PurchaseOrder'), false) THEN
                            RAISE EXCEPTION 'Handover notes retain settlement identity and exact delivery evidence'
                                USING ERRCODE = '23514', CONSTRAINT = 'CK_SaleDocuments_Handover';
                        END IF;
                        RETURN NEW;
                    END IF;
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
                CREATE OR REPLACE FUNCTION require_settlement_documents() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF NEW."Status" = 'Settled' AND OLD."Status" <> 'Settled' AND
                        (SELECT count(*) FROM "SaleDocuments" d JOIN "SalesOrderEvents" e
                            ON e."Id" = d."IssuingOperationId" AND e."SalesOrderId" = d."SalesOrderId"
                            WHERE d."SalesOrderId" = NEW."Id" AND e."Kind" = 'Settled' AND
                                e."Sequence" = NEW."Version" AND d."IssuedAt" = NEW."SettledAt") <> 2 THEN
                        RAISE EXCEPTION 'Settlement requires both issued documents in the same transaction'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_SalesOrders_Documents';
                    END IF;
                    RETURN NULL;
                END $$;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_SaleDocuments_SaleDocumentCorrections_CorrectionOperationId~",
                table: "SaleDocuments");

            migrationBuilder.DropTable(
                name: "SaleDocumentCorrections");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_SaleDocuments_Id_SalesOrderId",
                table: "SaleDocuments");

            migrationBuilder.DropIndex(
                name: "IX_SaleDocuments_CorrectionOperationId",
                table: "SaleDocuments");

            migrationBuilder.DropIndex(
                name: "IX_SaleDocuments_CorrectionOperationId_SalesOrderId",
                table: "SaleDocuments");

            migrationBuilder.DropIndex(
                name: "IX_SaleDocuments_SalesOrderId_Kind",
                table: "SaleDocuments");

            migrationBuilder.DropColumn(
                name: "CorrectionOperationId",
                table: "SaleDocuments");

            migrationBuilder.CreateIndex(
                name: "IX_SaleDocuments_SalesOrderId_Kind",
                table: "SaleDocuments",
                columns: ["SalesOrderId", "Kind"],
                unique: true);
        }
    }
}

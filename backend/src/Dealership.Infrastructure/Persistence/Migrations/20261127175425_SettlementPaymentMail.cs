using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dealership.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SettlementPaymentMail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SaleDeliveryMailOutbox_DeliveryOperationId_SalesOrderId",
                table: "SaleDeliveryMailOutbox");

            migrationBuilder.DropIndex(
                name: "IX_SaleDeliveryMailOutbox_SalesOrderId",
                table: "SaleDeliveryMailOutbox");

            migrationBuilder.CreateIndex(
                name: "IX_SaleDeliveryMailOutbox_DeliveryOperationId_SalesOrderId",
                table: "SaleDeliveryMailOutbox",
                columns: new[] { "DeliveryOperationId", "SalesOrderId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SaleDeliveryMailOutbox_SalesOrderId",
                table: "SaleDeliveryMailOutbox",
                column: "SalesOrderId");

            // The outbox now also holds the payment-confirmed mail, which settlement queues.
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
                            e."Kind" IN ('Settled', 'Delivered') AND NEW."RecipientEmail" = r."BuyerEmail" AND
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
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "SaleDeliveryMailOutbox" m
                        JOIN "SalesOrderEvents" e ON e."Id" = m."DeliveryOperationId" WHERE e."Kind" = 'Settled') THEN
                        RAISE EXCEPTION 'Cannot revert SettlementPaymentMail while settlement mail rows exist.';
                    END IF;
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

            migrationBuilder.DropIndex(
                name: "IX_SaleDeliveryMailOutbox_DeliveryOperationId_SalesOrderId",
                table: "SaleDeliveryMailOutbox");

            migrationBuilder.DropIndex(
                name: "IX_SaleDeliveryMailOutbox_SalesOrderId",
                table: "SaleDeliveryMailOutbox");

            migrationBuilder.CreateIndex(
                name: "IX_SaleDeliveryMailOutbox_DeliveryOperationId_SalesOrderId",
                table: "SaleDeliveryMailOutbox",
                columns: new[] { "DeliveryOperationId", "SalesOrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_SaleDeliveryMailOutbox_SalesOrderId",
                table: "SaleDeliveryMailOutbox",
                column: "SalesOrderId",
                unique: true);
        }
    }
}

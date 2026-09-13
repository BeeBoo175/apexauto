using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dealership.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RetentionSalesDisposal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "AccountHolder",
                table: "ReservationRefunds",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AddCheckConstraint(
                table: "ReservationRefunds",
                name: "CK_ReservationRefunds_Holder",
                sql: "\"AccountHolder\" IS NOT NULL OR \"State\" = 'Completed'");

            // The closed-customer minimization branch of the reservation guard also admits the retention executor:
            // an inactive reservation whose sale is terminal or absent may lose only its contact and NRIC copies.
            // The retention period itself is enforced by the executor, never by the trigger.
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION public.guard_reservation_update() RETURNS trigger
                LANGUAGE plpgsql SET search_path = pg_catalog AS $$
                BEGIN
                    IF OLD."IsActive" AND NOT NEW."IsActive" AND
                       (to_jsonb(OLD) - 'IsActive') = (to_jsonb(NEW) - 'IsActive') AND
                       (EXISTS (SELECT 1 FROM public."ReservationReleases" WHERE "ReservationId" = OLD."Id") OR
                           EXISTS (SELECT 1 FROM public."SalesOrders" WHERE "Id" = OLD."Id" AND "Status" = 'Settled')) THEN
                        RETURN NEW;
                    END IF;
                    -- An inactive reservation of a closed customer, or of a finished sale, may lose only its contact and NRIC copies.
                    IF NOT OLD."IsActive" AND NOT NEW."IsActive" AND
                       (to_jsonb(NEW) - 'BuyerEmail' - 'BuyerMobile' - 'BuyerNric') =
                           (to_jsonb(OLD) - 'BuyerEmail' - 'BuyerMobile' - 'BuyerNric') AND
                       (NEW."BuyerEmail" IS NULL OR NEW."BuyerEmail" = OLD."BuyerEmail") AND
                       (NEW."BuyerMobile" IS NULL OR NEW."BuyerMobile" = OLD."BuyerMobile") AND
                       (NEW."BuyerNric" IS NULL OR NEW."BuyerNric" = OLD."BuyerNric") AND
                       (EXISTS (SELECT 1 FROM public."AspNetUsers" u WHERE u."Id" = OLD."CustomerId" AND u."ClosedAt" IS NOT NULL) OR
                        NOT EXISTS (SELECT 1 FROM public."SalesOrders" s WHERE s."Id" = OLD."Id" AND
                            s."Status" NOT IN ('Delivered', 'Cancelled', 'Expired'))) THEN
                        RETURN NEW;
                    END IF;
                    RAISE EXCEPTION 'Only a retained release can free an active reservation'
                        USING ERRCODE = '23514', CONSTRAINT = 'CK_Reservations_Immutable';
                END $$;
                """);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restoring NOT NULL would need an invented holder name, so refuse instead.
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "ReservationRefunds" WHERE "AccountHolder" IS NULL) THEN
                        RAISE EXCEPTION 'Cannot revert RetentionSalesDisposal while minimized refunds exist.';
                    END IF;
                END $$;
                """);

            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION public.guard_reservation_update() RETURNS trigger
                LANGUAGE plpgsql SET search_path = pg_catalog AS $$
                BEGIN
                    IF OLD."IsActive" AND NOT NEW."IsActive" AND
                       (to_jsonb(OLD) - 'IsActive') = (to_jsonb(NEW) - 'IsActive') AND
                       (EXISTS (SELECT 1 FROM public."ReservationReleases" WHERE "ReservationId" = OLD."Id") OR
                           EXISTS (SELECT 1 FROM public."SalesOrders" WHERE "Id" = OLD."Id" AND "Status" = 'Settled')) THEN
                        RETURN NEW;
                    END IF;
                    -- A closed customer's inactive reservation may lose only its contact and NRIC copies.
                    IF NOT OLD."IsActive" AND NOT NEW."IsActive" AND
                       (to_jsonb(NEW) - 'BuyerEmail' - 'BuyerMobile' - 'BuyerNric') =
                           (to_jsonb(OLD) - 'BuyerEmail' - 'BuyerMobile' - 'BuyerNric') AND
                       (NEW."BuyerEmail" IS NULL OR NEW."BuyerEmail" = OLD."BuyerEmail") AND
                       (NEW."BuyerMobile" IS NULL OR NEW."BuyerMobile" = OLD."BuyerMobile") AND
                       (NEW."BuyerNric" IS NULL OR NEW."BuyerNric" = OLD."BuyerNric") AND
                       EXISTS (SELECT 1 FROM public."AspNetUsers" u WHERE u."Id" = OLD."CustomerId" AND u."ClosedAt" IS NOT NULL) THEN
                        RETURN NEW;
                    END IF;
                    RAISE EXCEPTION 'Only a retained release can free an active reservation'
                        USING ERRCODE = '23514', CONSTRAINT = 'CK_Reservations_Immutable';
                END $$;
                """);

            migrationBuilder.DropCheckConstraint(
                name: "CK_ReservationRefunds_Holder",
                table: "ReservationRefunds");

            migrationBuilder.AlterColumn<string>(
                name: "AccountHolder",
                table: "ReservationRefunds",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);
        }
    }
}

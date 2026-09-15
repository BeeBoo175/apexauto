using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dealership.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ErasedAccountsStayErased : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The CHECK constraints allow a closed row only without its identity, but they also allow an UPDATE that
            // clears the marker and refills every column at once. Each marker is one-way: once set it cannot move, and
            // the removed columns cannot be filled again. New per-table functions; no shared function is edited.
            migrationBuilder.Sql("""
                CREATE FUNCTION public.guard_closed_account_stays_closed() RETURNS trigger
                LANGUAGE plpgsql SET search_path = pg_catalog AS $$
                BEGIN
                    IF OLD."ClosedAt" IS NOT NULL AND (
                        NEW."ClosedAt" IS DISTINCT FROM OLD."ClosedAt" OR NEW."IsActive" OR
                        NEW."PasswordHash" IS NOT NULL OR NEW."Email" IS NOT NULL OR NEW."NormalizedEmail" IS NOT NULL OR
                        NEW."UserName" IS NOT NULL OR NEW."NormalizedUserName" IS NOT NULL OR
                        NEW."AccountUsername" IS NOT NULL OR NEW."PhoneNumber" IS NOT NULL OR NEW."EmailConfirmed") THEN
                        RAISE EXCEPTION 'A closed account cannot be reopened or given its identity back'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_Users_Closed';
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER closed_account_stays_closed BEFORE UPDATE ON "AspNetUsers"
                    FOR EACH ROW EXECUTE FUNCTION public.guard_closed_account_stays_closed();

                CREATE FUNCTION public.guard_erased_customer_stays_erased() RETURNS trigger
                LANGUAGE plpgsql SET search_path = pg_catalog AS $$
                BEGIN
                    IF OLD."ErasedAt" IS NOT NULL AND (
                        NEW."ErasedAt" IS DISTINCT FROM OLD."ErasedAt" OR NEW."LegalName" IS NOT NULL OR
                        NEW."Nric" IS NOT NULL OR NEW."Mobile" IS NOT NULL OR NEW."ResidentialAddress" IS NOT NULL) THEN
                        RAISE EXCEPTION 'An erased customer profile cannot be restored'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_CustomerProfiles_Erased';
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER erased_customer_stays_erased BEFORE UPDATE ON "CustomerProfiles"
                    FOR EACH ROW EXECUTE FUNCTION public.guard_erased_customer_stays_erased();

                CREATE FUNCTION public.guard_erased_staff_stays_erased() RETURNS trigger
                LANGUAGE plpgsql SET search_path = pg_catalog AS $$
                BEGIN
                    IF OLD."ErasedAt" IS NOT NULL AND (
                        NEW."ErasedAt" IS DISTINCT FROM OLD."ErasedAt" OR NEW."Name" IS NOT NULL OR
                        NEW."Mobile" IS NOT NULL) THEN
                        RAISE EXCEPTION 'An erased employee profile cannot be restored'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_StaffProfiles_Erased';
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER erased_staff_stays_erased BEFORE UPDATE ON "StaffProfiles"
                    FOR EACH ROW EXECUTE FUNCTION public.guard_erased_staff_stays_erased();

                CREATE FUNCTION public.guard_erased_receipt_needs_closed_target() RETURNS trigger
                LANGUAGE plpgsql SET search_path = pg_catalog AS $$
                BEGIN
                    IF NEW."Kind" = 'Erased' AND NOT EXISTS
                        (SELECT 1 FROM public."AspNetUsers" u WHERE u."Id" = NEW."TargetId" AND u."ClosedAt" IS NOT NULL) THEN
                        RAISE EXCEPTION 'An erasure receipt needs a closed account'
                            USING ERRCODE = '23514', CONSTRAINT = 'CK_AccountEvents_ErasedTarget';
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER erased_receipt_needs_closed_target BEFORE INSERT ON "AccountEvents"
                    FOR EACH ROW EXECUTE FUNCTION public.guard_erased_receipt_needs_closed_target();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER erased_receipt_needs_closed_target ON "AccountEvents";
                DROP FUNCTION public.guard_erased_receipt_needs_closed_target();
                DROP TRIGGER erased_staff_stays_erased ON "StaffProfiles";
                DROP FUNCTION public.guard_erased_staff_stays_erased();
                DROP TRIGGER erased_customer_stays_erased ON "CustomerProfiles";
                DROP FUNCTION public.guard_erased_customer_stays_erased();
                DROP TRIGGER closed_account_stays_closed ON "AspNetUsers";
                DROP FUNCTION public.guard_closed_account_stays_closed();
                """);
        }
    }
}

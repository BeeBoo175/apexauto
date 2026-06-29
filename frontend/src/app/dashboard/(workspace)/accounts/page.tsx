import { CustomerAccountsClientEntry } from "@/components/accounts/CustomerAccountsClientEntry";
import { StaffAccountsClientEntry } from "@/components/accounts/StaffAccountsClientEntry";
import { StaffEntry } from "@/components/staff/StaffEntry";
import { hasAccountAdministration } from "@/lib/accounts/staff-accounts";
import type { SearchParams } from "@/lib/query";

export default async function AccountsPage({
  searchParams,
}: {
  searchParams: Promise<SearchParams>;
}) {
  return (
    <StaffEntry
      path="/dashboard/accounts"
      searchParams={searchParams}
      access={hasAccountAdministration}
      deniedTitle="Accounts access unavailable"
      deniedDescription="Only Administrators can manage accounts."
    >
      {async (actor) => {
        const parameters = await searchParams;
        // The Staff view is the default; the Customers view is named in the address.
        return parameters.view === "customers" ? (
          <CustomerAccountsClientEntry
            key={crypto.randomUUID()}
            actor={actor}
            parameters={parameters}
          />
        ) : (
          <StaffAccountsClientEntry
            key={crypto.randomUUID()}
            actor={actor}
            parameters={parameters}
          />
        );
      }}
    </StaffEntry>
  );
}

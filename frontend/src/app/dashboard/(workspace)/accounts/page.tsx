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
      deniedDescription="Only Administrators can manage staff accounts."
    >
      {async (actor) => (
        <StaffAccountsClientEntry
          key={crypto.randomUUID()}
          actor={actor}
          parameters={await searchParams}
        />
      )}
    </StaffEntry>
  );
}

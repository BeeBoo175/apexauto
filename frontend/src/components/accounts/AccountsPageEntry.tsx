import { CustomerAccountsClientEntry } from "@/components/accounts/CustomerAccountsClientEntry";
import { StaffAccountsClientEntry } from "@/components/accounts/StaffAccountsClientEntry";
import { StaffEntry } from "@/components/staff/StaffEntry";
import {
  accountSelectionHref,
  type AccountSelectionPath,
  type AccountsView,
} from "@/lib/accounts/accounts-location";
import { hasAccountAdministration } from "@/lib/accounts/staff-accounts";
import type { SearchParams } from "@/lib/query";

const baseSelection: AccountSelectionPath = { id: "", section: "details" };

// Both views and their selected-account paths render the same role-appropriate workspace.
export function AccountsPageEntry({
  view,
  searchParams,
  selection = baseSelection,
}: {
  view: AccountsView;
  searchParams: Promise<SearchParams>;
  selection?: AccountSelectionPath;
}) {
  return (
    <StaffEntry
      path={accountSelectionHref(view, selection)}
      searchParams={searchParams}
      access={hasAccountAdministration}
    >
      {async (actor) => {
        const parameters = await searchParams;
        return view === "customers" ? (
          <CustomerAccountsClientEntry
            key={crypto.randomUUID()}
            actor={actor}
            parameters={parameters}
            selection={selection}
          />
        ) : (
          <StaffAccountsClientEntry
            key={crypto.randomUUID()}
            actor={actor}
            parameters={parameters}
            selection={selection}
          />
        );
      }}
    </StaffEntry>
  );
}

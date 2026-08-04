"use client";

import { useAccountsAddress } from "@/hooks/use-accounts-address";
import { customerAccountsRules } from "@/lib/accounts/accounts-location";
import { StaffCommandBar } from "@/components/staff/StaffPage";
import { LoadingProgress } from "@/components/ui/LoadingProgress";
import { RefreshButton } from "@/components/ui/RefreshButton";
import { useAccountCommand } from "@/hooks/use-account-command";
import { useCustomerAccountsList } from "@/hooks/use-customer-accounts-list";
import type { useCustomerAccountsEntry } from "@/hooks/use-customer-accounts-entry";
import { AccountsViewSwitch } from "./AccountsViewSwitch";
import { CustomerAccountDrawer } from "./CustomerAccountDrawer";
import { CustomerAccountsTable } from "./CustomerAccountsTable";

type CustomersEntry = NonNullable<ReturnType<typeof useCustomerAccountsEntry>["entry"]>;

export function CustomerAccountsWorkspace({ entry }: { entry: CustomersEntry }) {
  const address = useAccountsAddress(
    customerAccountsRules,
    {
      id: entry.id,
      section: entry.section,
      filters: entry.filters,
      page: entry.page,
    },
    currentList,
  );
  const list = useCustomerAccountsList(
    entry.initial,
    entry.filters,
    entry.page,
    address.onLocation,
  );
  const command = useAccountCommand();
  function currentList() {
    return list;
  }

  const reload = () => void list.load(list.active.filters, list.active.page, true, "replace");

  return (
    <>
      <LoadingProgress label="Loading customer accounts" active={list.busy} />
      <StaffCommandBar
        title="Accounts"
        actions={
          <>
            <AccountsViewSwitch view="customers" />
            <RefreshButton
              label="Refresh customer accounts"
              busy={list.busy}
              disabled={list.busy}
              onClick={() => void address.refresh()}
            />
          </>
        }
      />
      <CustomerAccountsTable list={list} selectedId={address.selection.id} onOpen={address.open} />
      <CustomerAccountDrawer
        row={address.selected}
        section={address.selection.section}
        onSection={address.choose}
        command={command}
        onClose={address.close}
        onChanged={reload}
      />
    </>
  );
}

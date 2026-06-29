"use client";

import { useState } from "react";
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
  const list = useCustomerAccountsList(entry.initial, entry.filters, entry.page);
  const command = useAccountCommand();
  const [selectedId, setSelectedId] = useState("");
  const selected = list.snapshot.items.find((row) => row.id === selectedId) ?? null;
  const reload = () => void list.load(list.active.filters, list.active.page, true);

  return (
    <>
      <LoadingProgress label="Loading customer accounts" active={list.busy} />
      <StaffCommandBar
        title="Customer accounts"
        actions={
          <>
            <AccountsViewSwitch view="customers" />
            <RefreshButton
              label="Refresh customer accounts"
              busy={list.busy}
              disabled={list.busy}
              onClick={reload}
            />
          </>
        }
      />
      <CustomerAccountsTable list={list} selectedId={selectedId} onOpen={setSelectedId} />
      <CustomerAccountDrawer
        row={selected}
        command={command}
        onClose={() => setSelectedId("")}
        onChanged={reload}
      />
    </>
  );
}

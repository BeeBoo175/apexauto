"use client";

import { useState } from "react";
import { useAccountsAddress } from "@/hooks/use-accounts-address";
import { staffAccountsRules } from "@/lib/accounts/accounts-location";
import { StaffCommandBar } from "@/components/staff/StaffPage";
import { Button } from "@/components/ui/Button";
import { LoadingProgress } from "@/components/ui/LoadingProgress";
import { RefreshButton } from "@/components/ui/RefreshButton";
import { useStaffAccountsList } from "@/hooks/use-staff-accounts-list";
import type { useStaffAccountsEntry } from "@/hooks/use-staff-accounts-entry";
import { useAccountCommand } from "@/hooks/use-account-command";
import { AccountsViewSwitch } from "./AccountsViewSwitch";
import { StaffAccountDrawer } from "./StaffAccountDrawer";
import { StaffAccountsTable } from "./StaffAccountsTable";
import { StaffInviteDialog } from "./StaffInviteDialog";

type AccountsEntry = NonNullable<ReturnType<typeof useStaffAccountsEntry>["entry"]>;

export function StaffAccountsWorkspace({ entry }: { entry: AccountsEntry }) {
  const address = useAccountsAddress(
    staffAccountsRules,
    {
      id: entry.id,
      section: entry.section,
      filters: entry.filters,
      page: entry.page,
    },
    currentList,
  );
  const list = useStaffAccountsList(entry.initial, entry.filters, entry.page, address.onLocation);
  const command = useAccountCommand();
  const [inviting, setInviting] = useState(false);
  // A revoked invitation leaves the list, which closes its drawer.
  function currentList() {
    return list;
  }

  const reload = () => void list.load(list.active.filters, list.active.page, true, "replace");

  return (
    <>
      <LoadingProgress label="Loading staff accounts" active={list.busy} />
      <StaffCommandBar
        title="Accounts"
        actions={
          <>
            <AccountsViewSwitch view="staff" />
            <RefreshButton
              label="Refresh staff accounts"
              busy={list.busy}
              disabled={list.busy}
              onClick={() => void address.refresh()}
            />
          </>
        }
      />
      <StaffAccountsTable
        list={list}
        branches={entry.branches}
        selectedId={address.selection.id}
        onOpen={address.open}
        actions={
          <Button size="compact" onClick={() => setInviting(true)}>
            Invite staff
          </Button>
        }
      />
      <StaffAccountDrawer
        row={address.selected}
        section={address.selection.section}
        onSection={address.choose}
        selfId={list.snapshot.actor.userId}
        branches={entry.branches}
        command={command}
        onClose={address.close}
        onChanged={reload}
      />
      <StaffInviteDialog
        open={inviting}
        branches={entry.branches}
        command={command}
        onClose={() => setInviting(false)}
        onInvited={reload}
      />
    </>
  );
}

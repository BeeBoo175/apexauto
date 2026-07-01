"use client";

import { useState } from "react";
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
  const list = useStaffAccountsList(entry.initial, entry.filters, entry.page);
  const command = useAccountCommand();
  const [selectedId, setSelectedId] = useState("");
  const [inviting, setInviting] = useState(false);
  // A revoked invitation leaves the list, which closes its drawer.
  const selected = list.snapshot.items.find((row) => row.id === selectedId) ?? null;
  const reload = () => void list.load(list.active.filters, list.active.page, true);

  return (
    <>
      <LoadingProgress label="Loading staff accounts" active={list.busy} />
      <StaffCommandBar
        title="Staff accounts"
        actions={
          <>
            <AccountsViewSwitch view="staff" />
            <RefreshButton
              label="Refresh staff accounts"
              busy={list.busy}
              disabled={list.busy}
              onClick={reload}
            />
          </>
        }
      />
      <StaffAccountsTable
        list={list}
        branches={entry.branches}
        selectedId={selectedId}
        onOpen={setSelectedId}
        actions={
          <Button size="compact" onClick={() => setInviting(true)}>
            Invite staff
          </Button>
        }
      />
      <StaffAccountDrawer
        row={selected}
        selfId={list.snapshot.actor.userId}
        branches={entry.branches}
        command={command}
        onClose={() => setSelectedId("")}
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

"use client";

import { StaffCommandBar } from "@/components/staff/StaffPage";
import { LoadingProgress } from "@/components/ui/LoadingProgress";
import { RefreshButton } from "@/components/ui/RefreshButton";
import { useStaffAccountsList } from "@/hooks/use-staff-accounts-list";
import type { useStaffAccountsEntry } from "@/hooks/use-staff-accounts-entry";
import { StaffAccountsTable } from "./StaffAccountsTable";

type AccountsEntry = NonNullable<ReturnType<typeof useStaffAccountsEntry>["entry"]>;

export function StaffAccountsWorkspace({ entry }: { entry: AccountsEntry }) {
  const list = useStaffAccountsList(entry.initial, entry.filters, entry.page);
  return (
    <>
      <LoadingProgress label="Loading staff accounts" active={list.busy} />
      <StaffCommandBar
        title="Staff accounts"
        actions={
          <RefreshButton
            label="Refresh staff accounts"
            busy={list.busy}
            disabled={list.busy}
            onClick={() => void list.load(list.active.filters, list.active.page, true)}
          />
        }
      />
      <StaffAccountsTable list={list} branches={entry.branches} />
    </>
  );
}

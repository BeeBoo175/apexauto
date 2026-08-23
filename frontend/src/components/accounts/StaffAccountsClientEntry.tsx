"use client";

import { StaffLoading } from "@/components/staff/StaffPage";
import { StaffReloadNotice } from "@/components/staff/StaffReloadNotice";
import { useStaffAccountsEntry } from "@/hooks/use-staff-accounts-entry";
import type { AccountSelectionPath } from "@/lib/accounts/accounts-location";
import type { CurrentActor } from "@/lib/auth/actor";
import type { SearchParams } from "@/lib/query";
import { StaffAccountsWorkspace } from "./StaffAccountsWorkspace";

export function StaffAccountsClientEntry({
  actor,
  parameters,
  selection,
}: {
  actor: CurrentActor;
  parameters: SearchParams;
  selection: AccountSelectionPath;
}) {
  const { entry, failed } = useStaffAccountsEntry(actor, parameters, selection);
  if (failed) {
    return <StaffReloadNotice title="Staff accounts" />;
  }
  if (!entry) {
    return <StaffLoading />;
  }
  return <StaffAccountsWorkspace entry={entry} />;
}

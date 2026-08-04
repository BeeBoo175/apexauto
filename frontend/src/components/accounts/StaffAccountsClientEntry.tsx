"use client";

import { StaffLoading, StaffNotice } from "@/components/staff/StaffPage";
import { Button } from "@/components/ui/Button";
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
  const { entry, failed, retry } = useStaffAccountsEntry(actor, parameters, selection);
  if (failed) {
    return (
      <StaffNotice title="Staff accounts">
        <Button size="compact" variant="secondary" onClick={() => void retry()}>
          Retry staff accounts
        </Button>
      </StaffNotice>
    );
  }
  if (!entry) {
    return <StaffLoading />;
  }
  return <StaffAccountsWorkspace entry={entry} />;
}

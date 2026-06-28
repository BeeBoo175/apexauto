"use client";

import { StaffLoading, StaffNotice } from "@/components/staff/StaffPage";
import { Button } from "@/components/ui/Button";
import { useStaffAccountsEntry } from "@/hooks/use-staff-accounts-entry";
import type { CurrentActor } from "@/lib/auth/actor";
import type { SearchParams } from "@/lib/query";
import { StaffAccountsWorkspace } from "./StaffAccountsWorkspace";

export function StaffAccountsClientEntry({
  actor,
  parameters,
}: {
  actor: CurrentActor;
  parameters: SearchParams;
}) {
  const { entry, failed, retry } = useStaffAccountsEntry(actor, parameters);
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

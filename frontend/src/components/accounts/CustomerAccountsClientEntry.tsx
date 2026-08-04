"use client";

import { StaffLoading, StaffNotice } from "@/components/staff/StaffPage";
import { Button } from "@/components/ui/Button";
import { useCustomerAccountsEntry } from "@/hooks/use-customer-accounts-entry";
import type { AccountSelectionPath } from "@/lib/accounts/accounts-location";
import type { CurrentActor } from "@/lib/auth/actor";
import type { SearchParams } from "@/lib/query";
import { CustomerAccountsWorkspace } from "./CustomerAccountsWorkspace";

export function CustomerAccountsClientEntry({
  actor,
  parameters,
  selection,
}: {
  actor: CurrentActor;
  parameters: SearchParams;
  selection: AccountSelectionPath;
}) {
  const { entry, failed, retry } = useCustomerAccountsEntry(actor, parameters, selection);
  if (failed) {
    return (
      <StaffNotice title="Customer accounts">
        <Button size="compact" variant="secondary" onClick={() => void retry()}>
          Retry customer accounts
        </Button>
      </StaffNotice>
    );
  }
  if (!entry) {
    return <StaffLoading />;
  }
  return <CustomerAccountsWorkspace entry={entry} />;
}

"use client";

import { StaffLoading, StaffNotice } from "@/components/staff/StaffPage";
import { Button } from "@/components/ui/Button";
import { useCustomerAccountsEntry } from "@/hooks/use-customer-accounts-entry";
import type { CurrentActor } from "@/lib/auth/actor";
import type { SearchParams } from "@/lib/query";
import { CustomerAccountsWorkspace } from "./CustomerAccountsWorkspace";

export function CustomerAccountsClientEntry({
  actor,
  parameters,
}: {
  actor: CurrentActor;
  parameters: SearchParams;
}) {
  const { entry, failed, retry } = useCustomerAccountsEntry(actor, parameters);
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

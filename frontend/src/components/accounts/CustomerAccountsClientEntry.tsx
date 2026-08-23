"use client";

import { StaffLoading } from "@/components/staff/StaffPage";
import { StaffReloadNotice } from "@/components/staff/StaffReloadNotice";
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
  const { entry, failed } = useCustomerAccountsEntry(actor, parameters, selection);
  if (failed) {
    return <StaffReloadNotice title="Customer accounts" />;
  }
  if (!entry) {
    return <StaffLoading />;
  }
  return <CustomerAccountsWorkspace entry={entry} />;
}

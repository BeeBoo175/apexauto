"use client";

import { StaffLoading, StaffNotice } from "@/components/staff/StaffPage";
import { Button } from "@/components/ui/Button";
import { useDealsClientEntry } from "@/hooks/use-deals-client-entry";
import type { CurrentActor } from "@/lib/auth/actor";
import type { SearchParams } from "@/lib/query";

export function DealsClientEntry({
  actor,
  parameters,
}: {
  actor: CurrentActor;
  parameters: SearchParams;
}) {
  const { entry, failed, retry } = useDealsClientEntry(actor, parameters);
  if (failed) {
    return (
      <StaffNotice title="Deals">
        <Button size="compact" variant="secondary" onClick={() => void retry()}>
          Retry deals
        </Button>
      </StaffNotice>
    );
  }
  if (!entry) {
    return <StaffLoading />;
  }
  const { Table, ...snapshot } = entry;
  return <Table actor={actor} {...snapshot} />;
}

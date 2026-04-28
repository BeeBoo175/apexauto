"use client";

import { StaffLoading, StaffNotice } from "@/components/staff/StaffPage";
import { Button } from "@/components/ui/Button";
import { useSalesClientEntry } from "@/hooks/use-sales-client-entry";
import type { CurrentActor } from "@/lib/auth/actor";
import type { SearchParams } from "@/lib/query";

export function SalesClientEntry({
  actor,
  parameters,
}: {
  actor: CurrentActor;
  parameters: SearchParams;
}) {
  const { entry, failed, retry } = useSalesClientEntry(parameters);
  if (failed) {
    return (
      <StaffNotice title="Sales">
        <Button size="compact" variant="secondary" onClick={() => void retry()}>
          Retry sales
        </Button>
      </StaffNotice>
    );
  }
  if (!entry) {
    return <StaffLoading />;
  }
  const { Workspace, ...snapshot } = entry;
  return <Workspace actor={actor} {...snapshot} />;
}

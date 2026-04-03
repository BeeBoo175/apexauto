"use client";

import { StaffLoading, StaffNotice } from "@/components/staff/StaffPage";
import { Button } from "@/components/ui/Button";
import { useWorkshopClientEntry } from "@/hooks/use-workshop-client-entry";
import type { CurrentActor } from "@/lib/auth/actor";
import type { SearchParams } from "@/lib/query";

export function WorkshopClientEntry({
  actor,
  parameters,
}: {
  actor: CurrentActor;
  parameters: SearchParams;
}) {
  const { entry, failed, retry } = useWorkshopClientEntry(actor, parameters);
  if (failed) {
    return (
      <StaffNotice title="Workshop">
        <Button size="compact" variant="secondary" onClick={() => void retry()}>
          Retry workshop
        </Button>
      </StaffNotice>
    );
  }
  if (!entry) {
    return <StaffLoading />;
  }
  if (entry.kind === "technician") {
    const { Workspace, props } = entry;
    return <Workspace actor={actor} {...props} />;
  }
  const { Workspace, props } = entry;
  return <Workspace actor={actor} {...props} />;
}

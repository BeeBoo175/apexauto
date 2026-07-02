"use client";

import { StaffLoading, StaffNotice } from "@/components/staff/StaffPage";
import { Button } from "@/components/ui/Button";
import { usePerformanceEntry } from "@/hooks/use-performance-entry";
import type { CurrentActor } from "@/lib/auth/actor";
import type { SearchParams } from "@/lib/query";
import { PerformanceWorkspace } from "./PerformanceWorkspace";

export function PerformanceClientEntry({
  actor,
  parameters,
}: {
  actor: CurrentActor;
  parameters: SearchParams;
}) {
  const { entry, failed, retry } = usePerformanceEntry(actor, parameters);
  if (failed) {
    return (
      <StaffNotice title="Performance">
        <Button size="compact" variant="secondary" onClick={() => void retry()}>
          Retry performance
        </Button>
      </StaffNotice>
    );
  }
  if (!entry) {
    return <StaffLoading />;
  }
  return <PerformanceWorkspace entry={entry} />;
}

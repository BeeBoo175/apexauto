"use client";

import { Button } from "@/components/ui/Button";
import { useWorkCalendarEntry } from "@/hooks/use-work-calendar-entry";
import type { CurrentActor } from "@/lib/auth/actor";
import type { SearchParams } from "@/lib/query";
import { StaffLoading, StaffNotice } from "./StaffPage";
import { WorkCalendarWorkspace } from "./WorkCalendarWorkspace";

export function WorkCalendarClientEntry({
  actor,
  parameters,
}: {
  actor: CurrentActor;
  parameters: SearchParams;
}) {
  const { entry, failed, retry } = useWorkCalendarEntry(actor, parameters);
  if (failed) {
    return (
      <StaffNotice title="Work calendar">
        <Button size="compact" variant="secondary" onClick={() => void retry()}>
          Retry work calendar
        </Button>
      </StaffNotice>
    );
  }
  if (!entry) {
    return <StaffLoading />;
  }
  return <WorkCalendarWorkspace actor={actor} entry={entry} />;
}

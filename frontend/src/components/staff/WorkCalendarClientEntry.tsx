"use client";

import { useWorkCalendarEntry } from "@/hooks/use-work-calendar-entry";
import type { CurrentActor } from "@/lib/auth/actor";
import type { SearchParams } from "@/lib/query";
import { StaffLoading } from "./StaffPage";
import { StaffReloadNotice } from "./StaffReloadNotice";
import { WorkCalendarWorkspace } from "./WorkCalendarWorkspace";

export function WorkCalendarClientEntry({
  actor,
  parameters,
}: {
  actor: CurrentActor;
  parameters: SearchParams;
}) {
  const { entry, failed } = useWorkCalendarEntry(actor, parameters);
  if (failed) {
    return <StaffReloadNotice title="Work calendar" />;
  }
  if (!entry) {
    return <StaffLoading />;
  }
  return <WorkCalendarWorkspace actor={actor} entry={entry} />;
}

"use client";

import { StaffLoading } from "@/components/staff/StaffPage";
import { StaffReloadNotice } from "@/components/staff/StaffReloadNotice";
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
  const { entry, failed } = useWorkshopClientEntry(actor, parameters);
  if (failed) {
    return <StaffReloadNotice title="Workshop" />;
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

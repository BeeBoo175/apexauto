"use client";

import { useIntakeClientEntry } from "@/hooks/use-intake-client-entry";
import type { CurrentActor } from "@/lib/auth/actor";
import type { SearchParams } from "@/lib/query";
import { IntakeWorkspace } from "./IntakeWorkspace";
import { StaffLoading } from "./StaffPage";
import { StaffReloadNotice } from "./StaffReloadNotice";

export function IntakeClientEntry({
  actor,
  parameters,
  selectedId,
}: {
  actor: CurrentActor;
  parameters: SearchParams;
  selectedId: string;
}) {
  const { entry, failed } = useIntakeClientEntry(actor, parameters, selectedId);
  if (failed) {
    return <StaffReloadNotice title="Intake" />;
  }
  if (!entry) {
    return <StaffLoading />;
  }
  return <IntakeWorkspace actor={actor} initial={entry.snapshot} initialView={entry.view} />;
}

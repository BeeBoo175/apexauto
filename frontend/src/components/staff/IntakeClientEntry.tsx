"use client";

import { Button } from "@/components/ui/Button";
import { useIntakeClientEntry } from "@/hooks/use-intake-client-entry";
import type { CurrentActor } from "@/lib/auth/actor";
import type { SearchParams } from "@/lib/query";
import { IntakeWorkspace } from "./IntakeWorkspace";
import { StaffLoading, StaffNotice } from "./StaffPage";

export function IntakeClientEntry({
  actor,
  parameters,
}: {
  actor: CurrentActor;
  parameters: SearchParams;
}) {
  const { entry, failed, retry } = useIntakeClientEntry(actor, parameters);
  if (failed) {
    return (
      <StaffNotice title="Intake appointments">
        <Button size="compact" variant="secondary" onClick={() => void retry()}>
          Retry appointments
        </Button>
      </StaffNotice>
    );
  }
  if (!entry) {
    return <StaffLoading />;
  }
  return <IntakeWorkspace actor={actor} initial={entry.snapshot} initialView={entry.view} />;
}

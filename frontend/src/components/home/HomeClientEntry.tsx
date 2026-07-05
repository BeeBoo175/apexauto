"use client";

import { StaffLoading, StaffNotice } from "@/components/staff/StaffPage";
import { Button } from "@/components/ui/Button";
import { useHomeEntry } from "@/hooks/use-home-entry";
import type { CurrentActor } from "@/lib/auth/actor";
import type { SearchParams } from "@/lib/query";
import { AgentHome } from "./AgentHome";
import { HomeWorkspace } from "./HomeWorkspace";

export function HomeClientEntry({
  actor,
  parameters,
}: {
  actor: CurrentActor;
  parameters: SearchParams;
}) {
  const { entry, failed, retry } = useHomeEntry(actor, parameters);
  if (failed) {
    return (
      <StaffNotice title="Home">
        <Button size="compact" variant="secondary" onClick={() => void retry()}>
          Retry home
        </Button>
      </StaffNotice>
    );
  }
  if (!entry) {
    return <StaffLoading />;
  }
  if (entry.kind === "agent") {
    return <AgentHome entry={entry} />;
  }
  return <HomeWorkspace entry={entry} />;
}

"use client";

import { StaffLoading } from "@/components/staff/StaffPage";
import { StaffReloadNotice } from "@/components/staff/StaffReloadNotice";
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
  const { entry, failed } = useHomeEntry(actor, parameters);
  if (failed) {
    return <StaffReloadNotice title="Home" />;
  }
  if (!entry) {
    return <StaffLoading />;
  }
  if (entry.kind === "agent") {
    return <AgentHome entry={entry} />;
  }
  return <HomeWorkspace entry={entry} />;
}

"use client";

import { useState } from "react";
import { StaffLoading } from "@/components/staff/StaffPage";
import { StaffReloadNotice } from "@/components/staff/StaffReloadNotice";
import { usePerformanceAddress, PerformanceAddressContext } from "@/hooks/use-performance-address";
import { usePerformanceEntry } from "@/hooks/use-performance-entry";
import type { CurrentActor } from "@/lib/auth/actor";
import type { PerformanceParameters } from "@/lib/performance/performance-location";
import type { SearchParams } from "@/lib/query";
import { PerformanceWorkspace } from "./PerformanceWorkspace";

function flatParameters(parameters: SearchParams) {
  const flat: PerformanceParameters = {};
  for (const [key, value] of Object.entries(parameters)) {
    const first = Array.isArray(value) ? value[0] : value;
    if (first) {
      flat[key] = first;
    }
  }
  return flat;
}

// The address owner. Back and Forward open the destination through a fresh entry read, so a failed read keeps the
// requested address and its Retry targets that destination.
export function PerformanceClientEntry({
  actor,
  parameters,
}: {
  actor: CurrentActor;
  parameters: SearchParams;
}) {
  const [opened, setOpened] = useState({ parameters: flatParameters(parameters), generation: 0 });
  const address = usePerformanceAddress(opened.parameters, (target) =>
    setOpened((previous) => ({
      parameters: target.parameters,
      generation: previous.generation + 1,
    })),
  );
  return (
    <PerformanceAddressContext.Provider
      value={{ parameters: opened.parameters, patch: address.patch }}
    >
      <PerformanceEntryBody key={opened.generation} actor={actor} parameters={opened.parameters} />
    </PerformanceAddressContext.Provider>
  );
}

function PerformanceEntryBody({
  actor,
  parameters,
}: {
  actor: CurrentActor;
  parameters: PerformanceParameters;
}) {
  const { entry, failed } = usePerformanceEntry(actor, parameters);
  if (failed) {
    return <StaffReloadNotice title="Performance" />;
  }
  if (!entry) {
    return <StaffLoading />;
  }
  return <PerformanceWorkspace entry={entry} />;
}

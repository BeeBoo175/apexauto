"use client";

import { useSectionRead } from "@/hooks/use-section-read";
import { readUnscheduledWork } from "@/hooks/use-work-calendar-entry";
import { privateApiFetch } from "@/lib/api-browser";
import { actorIdentity, type CurrentActor } from "@/lib/auth/actor";
import type { SalesQueue } from "@/lib/sales/contracts";
import { parseSalesQueue } from "@/lib/sales/parse";
import { queueQuery, salesQueuePath } from "@/lib/sales/query";
import { workCalendarParams, type UnscheduledWorkPage } from "@/lib/staff/work-calendar";

// A sales agent's own figures for Home: their sales queue counts and the first page of their open work, both through
// the reads their Sales queue and Work calendar already use. Failed refreshes keep the last confirmed figures. The
// entry reads both through `readAgentWork`, so Home reveals once with everything it needs.
export async function readAgentWork(actor: CurrentActor, signal: AbortSignal) {
  const [queue, work] = await Promise.all([
    privateApiFetch(`${salesQueuePath}?${queueQuery("Handling")}`, parseSalesQueue, { signal }),
    readUnscheduledWork(actor, workCalendarParams({}), signal),
  ]);
  if (!queue.data || !work.data) {
    throw new Error("Home unavailable");
  }
  return { queue: queue.data, work: work.data };
}

export function useAgentHome(
  actor: CurrentActor,
  initialQueue: SalesQueue,
  initialWork: UnscheduledWorkPage,
  refreshes: number,
) {
  // The entry already read both, so the first mount shows them and reads again only on refresh or retry.
  const { data, busy, failed, retry } = useSectionRead({
    key: actorIdentity(actor),
    refreshes,
    failure: "Your work could not be loaded. Refresh to try again.",
    initial: { queue: initialQueue, work: initialWork },
    read: (signal) => readAgentWork(actor, signal),
  });
  return { queue: data?.queue ?? null, work: data?.work ?? null, busy, failed, retry };
}

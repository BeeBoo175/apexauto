"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { useToast } from "@/components/ui/Toast";
import { readUnscheduledWork } from "@/hooks/use-work-calendar-entry";
import { privateApiFetch } from "@/lib/api-browser";
import type { CurrentActor } from "@/lib/auth/actor";
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
  const [queue, setQueue] = useState<SalesQueue | null>(initialQueue);
  const [work, setWork] = useState<UnscheduledWorkPage | null>(initialWork);
  const [busy, setBusy] = useState(false);
  const [failed, setFailed] = useState(false);
  const [attempt, setAttempt] = useState(0);
  const toast = useToast();
  const reported = useRef(false);
  // The entry already read both, so the first mount shows them and reads again only on refresh or retry.
  const skipFirst = useRef(true);

  useEffect(() => {
    if (skipFirst.current) {
      skipFirst.current = false;
      return;
    }
    const controller = new AbortController();
    let current = true;
    async function load() {
      setBusy(true);
      try {
        const next = await readAgentWork(actor, controller.signal);
        if (!current) {
          return;
        }
        setQueue(next.queue);
        setWork(next.work);
        setFailed(false);
        reported.current = false;
      } catch {
        if (!current) {
          return;
        }
        setFailed(true);
        if (!reported.current) {
          reported.current = true;
          toast.error("Your work could not be loaded. Refresh to try again.");
        }
      } finally {
        if (current) {
          setBusy(false);
        }
      }
    }
    void load();
    return () => {
      current = false;
      controller.abort();
    };
    // The toast is stable; the read depends on the viewer, refreshes and retries.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [actor, refreshes, attempt]);

  const retry = useCallback(() => setAttempt((value) => value + 1), []);
  return { queue, work, busy, failed, retry };
}

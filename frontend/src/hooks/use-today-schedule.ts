"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { useToast } from "@/components/ui/Toast";
import { readScheduledWork } from "@/hooks/use-work-calendar-entry";
import type { CurrentActor } from "@/lib/auth/actor";
import { calendarDate } from "@/lib/calendar";
import { workCalendarParams, type ScheduledWork } from "@/lib/staff/work-calendar";

// Today's appointments and test drives, read through the Work calendar's own scoped read. That read covers the
// calendar month around today, so the same snapshot also serves the Work calendar page; Home keeps only today.
export async function readTodaySchedule(
  actor: CurrentActor,
  branchId: string,
  signal: AbortSignal,
) {
  const today = calendarDate();
  const filters = { ...workCalendarParams({}), date: today, branchId };
  const result = await readScheduledWork(actor, filters, signal);
  if (result.status !== 200 || !result.data) {
    throw new Error("Schedule unavailable");
  }
  return result.data.items
    .filter((item) => calendarDate(new Date(item.startsAt)) === today)
    .sort((first, second) => Date.parse(first.startsAt) - Date.parse(second.startsAt));
}

// The schedule section's state. It opens with the entry's read and reads again on refresh, retry or a branch change,
// keeping the last confirmed schedule when a refresh fails.
export function useTodaySchedule(
  actor: CurrentActor,
  branchId: string,
  refreshes: number,
  initial: ScheduledWork[] | null,
) {
  const [items, setItems] = useState<ScheduledWork[] | null>(initial);
  const [busy, setBusy] = useState(initial === null);
  const [failed, setFailed] = useState(false);
  const [attempt, setAttempt] = useState(0);
  const toast = useToast();
  const reported = useRef(false);
  const skipFirst = useRef(initial !== null);

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
        const next = await readTodaySchedule(actor, branchId, controller.signal);
        if (!current) {
          return;
        }
        setItems(next);
        setFailed(false);
        reported.current = false;
      } catch {
        if (!current) {
          return;
        }
        setFailed(true);
        if (!reported.current) {
          reported.current = true;
          toast.error("Today's schedule could not be loaded. Retry that section.");
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
    // The toast is stable; the read depends on the viewer, branch, refreshes and retries.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [actor, branchId, refreshes, attempt]);

  const retry = useCallback(() => setAttempt((value) => value + 1), []);
  return { data: items, busy, failed, retry };
}

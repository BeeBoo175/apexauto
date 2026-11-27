"use client";

import { readScheduledWork } from "@/hooks/use-work-calendar-entry";
import { useSectionRead } from "@/hooks/use-section-read";
import { apiReadError } from "@/lib/api";
import { actorIdentity, type CurrentActor } from "@/lib/auth/actor";
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
    throw apiReadError("Schedule unavailable", result);
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
  return useSectionRead({
    key: `${actorIdentity(actor)}#${branchId}`,
    refreshes,
    failure: "Today's schedule could not be loaded. Reload the page to try again.",
    initial,
    read: (signal) => readTodaySchedule(actor, branchId, signal),
  });
}

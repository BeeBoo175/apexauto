"use client";

import { apiFetch, privateApiFetch } from "@/lib/api-browser";
import { parsePage, unavailableResult } from "@/lib/api";
import { actorIdentity, type CurrentActor } from "@/lib/auth/actor";
import { parseBranch } from "@/lib/branches";
import type { SearchParams } from "@/lib/query";
import {
  parseUnscheduledWork,
  parseWorkCalendar,
  workCalendarParams,
  workCalendarQuery,
  type WorkCalendarFilters,
  type WorkCalendarSnapshot,
  type UnscheduledWorkPage,
} from "@/lib/staff/work-calendar";
import { calendarRange } from "@/lib/calendar";
import { useStaffClientEntry } from "./use-staff-client-entry";

export async function readScheduledWork(
  actor: CurrentActor,
  filters: WorkCalendarFilters,
  signal: AbortSignal,
) {
  const range = calendarRange(filters.date);
  return privateApiFetch(
    `/api/staff/work-calendar?${workCalendarQuery(filters)}`,
    (value) => {
      const data = parseWorkCalendar(value);
      return data &&
        matchesScope(data, actor, filters) &&
        data.from === range.from &&
        data.to === range.to
        ? data
        : null;
    },
    { signal },
  );
}

export async function readUnscheduledWork(
  actor: CurrentActor,
  filters: WorkCalendarFilters,
  signal: AbortSignal,
) {
  return privateApiFetch(
    `/api/staff/work-calendar/unscheduled?${workCalendarQuery(filters, true)}`,
    (value) => {
      const data = parseUnscheduledWork(value);
      return data &&
        matchesScope(data, actor, filters) &&
        data.page === filters.page &&
        data.pageSize === filters.pageSize &&
        data.total ===
          data.counts[({ "": "all", Workshop: "workshop", Sale: "sale" } as const)[filters.kind]]
        ? data
        : null;
    },
    { signal },
  );
}

function matchesScope(
  data: WorkCalendarSnapshot | UnscheduledWorkPage,
  actor: CurrentActor,
  filters: WorkCalendarFilters,
) {
  return (
    actorIdentity(data.actor) === actorIdentity(actor) &&
    data.branchId === (filters.branchId || actor.branchId) &&
    data.employee === ""
  );
}

export function readCalendarBranches(page: number, signal: AbortSignal) {
  return apiFetch(
    `/api/intake/branches?page=${page}&pageSize=25`,
    (value) => {
      const data = parsePage(value, parseBranch);
      return data?.page === page && data.pageSize === 25 ? data : null;
    },
    { signal },
  );
}

export function useWorkCalendarEntry(actor: CurrentActor, parameters: SearchParams) {
  return useStaffClientEntry(async (signal) => {
    const filters = workCalendarParams(parameters);
    if (actor.role !== "GeneralManager") {
      filters.branchId = "";
    }
    if (actor.role !== "BranchManager" && actor.role !== "GeneralManager") {
      filters.assignment = "All";
      filters.kind = "";
    }
    const [scheduled, unscheduled, branches] = await Promise.all([
      readScheduledWork(actor, filters, signal).catch(() =>
        unavailableResult<WorkCalendarSnapshot>(),
      ),
      readUnscheduledWork(actor, filters, signal).catch(() =>
        unavailableResult<UnscheduledWorkPage>(),
      ),
      actor.role === "GeneralManager" ? readCalendarBranches(1, signal).catch(() => null) : null,
    ]);
    return { filters, scheduled, unscheduled, branches: branches?.data ?? null };
  }, "Work calendar could not be opened. Retry to load your work.");
}

export type WorkCalendarEntry = NonNullable<ReturnType<typeof useWorkCalendarEntry>["entry"]>;

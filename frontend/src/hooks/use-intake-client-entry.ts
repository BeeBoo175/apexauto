"use client";

import { useEffect } from "react";
import { useToast } from "@/components/ui/Toast";
import { useStaffClientEntry } from "./use-staff-client-entry";
import { apiFetch, privateApiFetch } from "@/lib/api-browser";
import { parsePage } from "@/lib/api";
import type { CurrentActor } from "@/lib/auth/actor";
import { isUuid } from "@/lib/auth/contracts";
import { parseBranch } from "@/lib/branches";
import type { SearchParams } from "@/lib/query";
import {
  calendarParams,
  calendarQuery,
  calendarReadRange,
  parseCalendar,
} from "@/lib/staff/calendar";
import { parseDetail, parseWorklistItem } from "@/lib/staff/parse";
import { staffParams, staffQuery } from "@/lib/staff/query";
import type { IntakeViewComponent, IntakeWorkspaceSnapshot } from "./use-intake-workspace";

interface IntakeEntry {
  snapshot: IntakeWorkspaceSnapshot;
  view: IntakeViewComponent;
}

export function useIntakeClientEntry(actor: CurrentActor, parameters: SearchParams) {
  const { error } = useToast();
  const result = useStaffClientEntry<IntakeEntry>(async (signal) => {
    const listView = parameters.view === "list";
    const { filters, page } = staffParams(parameters);
    const calendarFilters = calendarParams(parameters);
    const selectedId = isUuid(parameters.appointment) ? parameters.appointment : "";

    const [calendar, worklist, branches, selected, view] = await Promise.all([
      listView
        ? null
        : privateApiFetch(
            `/api/staff/intake/calendar?${calendarQuery(calendarFilters)}`,
            (value) => parseCalendar(value, calendarReadRange(calendarFilters)),
            { signal },
            { maximumResponseBytes: 1024 * 1024 },
          ),
      listView
        ? privateApiFetch(
            `/api/staff/intake/appointments?${staffQuery(filters, page)}`,
            (value) => parsePage(value, parseWorklistItem),
            { signal },
          )
        : null,
      actor.role === "GeneralManager"
        ? apiFetch(
            "/api/intake/branches?page=1&pageSize=25",
            (value) => parsePage(value, parseBranch),
            { signal },
          ).catch(() => null)
        : null,
      selectedId
        ? privateApiFetch(
            `/api/staff/intake/appointments/${selectedId}`,
            (value) => {
              const detail = parseDetail(value);
              return detail?.id === selectedId ? detail : null;
            },
            { signal },
          ).catch(() => null)
        : null,
      prepareView(listView),
    ]);
    const initial = listView ? worklist : calendar;
    if (
      !initial ||
      (listView &&
        worklist?.data &&
        (worklist.data.page !== page || worklist.data.pageSize !== filters.pageSize))
    ) {
      throw new Error("Appointments unavailable");
    }
    return {
      view,
      snapshot: {
        view: listView ? "list" : "calendar",
        calendar:
          calendar?.status === 422
            ? {
                ...calendar,
                message:
                  "More than 1,000 appointments match. Filter by branch or status, or switch to Table.",
              }
            : calendar,
        worklist,
        calendarFilters,
        filters,
        page,
        branches: branches?.data ?? null,
        selectedId,
        selected: selectedId
          ? (selected ?? { status: 502, data: null, message: "", uncertain: false })
          : null,
      },
    };
  }, "Appointments could not be loaded. Retry to open the workspace.");
  const snapshot = result.entry?.snapshot;
  useEffect(() => {
    if (snapshot?.selectedId && !snapshot.selected?.data) {
      error("The selected appointment could not be opened. Retry it from the drawer.");
    }
  }, [snapshot, error]);
  return result;
}

async function prepareView(listView: boolean): Promise<IntakeViewComponent> {
  if (listView) {
    const { IntakeWorklist } = await import("@/components/staff/IntakeWorklist");
    return { view: "list", Component: IntakeWorklist };
  }
  const { IntakeCalendar } = await import("@/components/staff/IntakeCalendar");
  return { view: "calendar", Component: IntakeCalendar };
}

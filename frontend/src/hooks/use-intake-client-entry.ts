"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { useToast } from "@/components/ui/Toast";
import { apiFetch } from "@/lib/api-browser";
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
  const [entry, setEntry] = useState<IntakeEntry | null>(null);
  const [failed, setFailed] = useState(false);
  const controller = useRef<AbortController | null>(null);
  const { error } = useToast();

  const load = useCallback(async () => {
    if (controller.current) {
      return;
    }
    const attempt = new AbortController();
    controller.current = attempt;
    setFailed(false);
    const listView = parameters.view === "list";
    const { filters, page } = staffParams(parameters);
    const calendarFilters = calendarParams(parameters);
    const selectedId = isUuid(parameters.appointment) ? parameters.appointment : "";
    const privateRead = { requireActor: true };

    try {
      const [calendar, worklist, branches, selected, view] = await Promise.all([
        listView
          ? null
          : apiFetch(
              `/api/staff/intake/calendar?${calendarQuery(calendarFilters)}`,
              (value) => parseCalendar(value, calendarReadRange(calendarFilters)),
              { signal: attempt.signal },
              { ...privateRead, maximumResponseBytes: 1024 * 1024 },
            ),
        listView
          ? apiFetch(
              `/api/staff/intake/appointments?${staffQuery(filters, page)}`,
              (value) => parsePage(value, parseWorklistItem),
              { signal: attempt.signal },
              privateRead,
            )
          : null,
        actor.role === "GeneralManager"
          ? apiFetch(
              "/api/intake/branches?page=1&pageSize=25",
              (value) => parsePage(value, parseBranch),
              { signal: attempt.signal },
            ).catch(() => null)
          : null,
        selectedId
          ? apiFetch(
              `/api/staff/intake/appointments/${selectedId}`,
              (value) => {
                const detail = parseDetail(value);
                return detail?.id === selectedId ? detail : null;
              },
              { signal: attempt.signal },
              privateRead,
            ).catch(() => null)
          : null,
        prepareView(listView),
      ]);
      if (attempt.signal.aborted || controller.current !== attempt) {
        return;
      }
      const initial = listView ? worklist : calendar;
      if (!initial || (listView && worklist?.data && worklist.data.page !== page)) {
        throw new Error("Appointments unavailable");
      }
      if (selectedId && !selected?.data) {
        error("The selected appointment could not be opened. Retry it from the drawer.");
      }
      setEntry({
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
      });
    } catch {
      if (!attempt.signal.aborted && controller.current === attempt) {
        setFailed(true);
        error("Appointments could not be loaded. Retry to open the workspace.");
      }
    } finally {
      if (controller.current === attempt) {
        controller.current = null;
      }
    }
  }, [actor, parameters, error]);

  useEffect(() => {
    let active = true;
    queueMicrotask(() => {
      if (active) {
        void load();
      }
    });
    return () => {
      active = false;
      controller.current?.abort();
      controller.current = null;
    };
  }, [load]);

  return { entry, failed, retry: load };
}

async function prepareView(listView: boolean): Promise<IntakeViewComponent> {
  if (listView) {
    const { IntakeWorklist } = await import("@/components/staff/IntakeWorklist");
    return { view: "list", Component: IntakeWorklist };
  }
  const { IntakeCalendar } = await import("@/components/staff/IntakeCalendar");
  return { view: "calendar", Component: IntakeCalendar };
}

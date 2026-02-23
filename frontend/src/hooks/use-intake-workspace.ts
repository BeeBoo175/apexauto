"use client";

import { useEffect, useEffectEvent, useRef, useState } from "react";
import { useStaffCalendar } from "./use-staff-calendar";
import { useStaffWorklist } from "./use-staff-worklist";
import { useStaffAppointmentDrawer } from "./use-staff-appointment-drawer";
import {
  calendarHref,
  calendarParams,
  type CalendarFilters,
  type CalendarSnapshot,
} from "@/lib/staff/calendar";
import { staffParams, staffViewQuery } from "@/lib/staff/query";
import { tableViewHref, calendarViewHref } from "@/lib/staff/view-navigation";
import type { IntakeView } from "@/lib/staff/view-memory";
import type { ApiResult, Page } from "@/lib/api";
import type { Branch } from "@/lib/branches";
import type { AppointmentDetail, StaffFilters, WorklistItem } from "@/lib/staff/contracts";

export interface IntakeWorkspaceSnapshot {
  view: IntakeView;
  calendar: ApiResult<CalendarSnapshot> | null;
  worklist: ApiResult<Page<WorklistItem>> | null;
  calendarFilters: CalendarFilters;
  filters: StaffFilters;
  page: number;
  branches: Page<Branch> | null;
  selectedId: string;
  selected: ApiResult<AppointmentDetail> | null;
}

const unloaded = { status: 200, data: null, message: "", uncertain: false };

export function useIntakeWorkspace(initial: IntakeWorkspaceSnapshot) {
  const calendar = useStaffCalendar(
    initial.calendar ?? unloaded,
    initial.calendarFilters,
    initial.branches,
  );
  const worklist = useStaffWorklist(
    initial.worklist ?? unloaded,
    initial.filters,
    initial.page,
    initial.branches,
  );
  const drawer = useStaffAppointmentDrawer(initial.selectedId, initial.selected);
  const [view, setView] = useState(initial.view);
  const [pending, setPending] = useState<IntakeView | null>(null);
  const attempt = useRef<symbol | null>(null);
  const confirmedHref = useRef("");
  const busy = calendar.busy || worklist.busy;
  const destinations =
    view === "calendar"
      ? { calendar: calendarHref(calendar.active), list: tableViewHref(calendar.active) }
      : {
          calendar: calendarViewHref(worklist.active.filters),
          list: `/dashboard/intake?${staffViewQuery(worklist.active.filters, worklist.active.page)}`,
        };
  const href = destinations[view];

  useEffect(() => {
    const url = new URL(href, window.location.origin);
    if (drawer.id) {
      url.searchParams.set("appointment", drawer.id);
    }
    confirmedHref.current = `${url.pathname}${url.search}`;
  }, [href, drawer.id]);

  useEffect(
    () => () => {
      attempt.current = null;
    },
    [],
  );

  async function navigate(destination: string, history: "push" | "replace" = "push") {
    if (drawer.id || (busy && !attempt.current)) {
      return;
    }
    calendar.cancel();
    worklist.cancel();
    const currentAttempt = Symbol();
    attempt.current = currentAttempt;
    const url = new URL(destination, window.location.origin);
    const params = Object.fromEntries(url.searchParams);
    const nextView = params.view === "list" ? "list" : "calendar";
    setPending(nextView);
    const nextCalendar = calendarParams(params);
    const nextWorklist = staffParams(params);
    const success =
      nextView === "calendar"
        ? await calendar.load(nextCalendar, false)
        : await worklist.load(nextWorklist.filters, nextWorklist.page, false);
    if (attempt.current !== currentAttempt) {
      return;
    }
    attempt.current = null;
    setPending(null);
    if (!success) {
      return;
    }
    const nextHref =
      nextView === "calendar"
        ? calendarHref(nextCalendar)
        : `/dashboard/intake?${staffViewQuery(nextWorklist.filters, nextWorklist.page)}`;
    window.history[history === "push" ? "pushState" : "replaceState"](null, "", nextHref);
    setView(nextView);
  }

  const restore = useEffectEvent(() => {
    if (window.location.pathname !== "/dashboard/intake") {
      return;
    }
    const destination = window.location.href;
    window.history.replaceState(null, "", confirmedHref.current);
    void navigate(destination, "replace");
  });

  const cancel = useEffectEvent((event: KeyboardEvent) => {
    if (event.key !== "Escape" || !attempt.current) {
      return;
    }
    attempt.current = null;
    calendar.cancel();
    worklist.cancel();
    setPending(null);
  });

  useEffect(() => {
    window.addEventListener("popstate", restore);
    window.addEventListener("keydown", cancel);
    return () => {
      window.removeEventListener("popstate", restore);
      window.removeEventListener("keydown", cancel);
    };
  }, []);

  return { view, pending, busy, calendar, worklist, drawer, destinations, navigate };
}

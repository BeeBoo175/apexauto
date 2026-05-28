"use client";

import { useEffect, useEffectEvent, useRef, useState } from "react";
import { useStaffCalendar } from "./use-staff-calendar";
import { useStaffWorklist } from "./use-staff-worklist";
import { useStaffAppointmentDrawer } from "./use-staff-appointment-drawer";
import { useToast } from "@/components/ui/Toast";
import { useGuardedAction } from "./use-guarded-action";
import type { IntakeCalendar } from "@/components/staff/IntakeCalendar";
import type { IntakeWorklist } from "@/components/staff/IntakeWorklist";
import type { IntakeAgentQueue } from "@/components/staff/IntakeAgentQueue";
import {
  calendarHref,
  calendarParams,
  type CalendarFilters,
  type CalendarSnapshot,
} from "@/lib/staff/calendar";
import {
  appointmentQueue,
  intakeQueueParams,
  staffParams,
  staffViewQuery,
} from "@/lib/staff/query";
import { tableViewHref, calendarViewHref, queueViewHref } from "@/lib/staff/view-navigation";
import { intakeView, type IntakeView } from "@/lib/staff/view-memory";
import type { ApiResult, Page } from "@/lib/api";
import type { Branch } from "@/lib/branches";
import type { AppointmentDetail, StaffFilters, WorklistPage } from "@/lib/staff/contracts";

export interface IntakeWorkspaceSnapshot {
  view: IntakeView;
  calendar: ApiResult<CalendarSnapshot> | null;
  worklist: ApiResult<WorklistPage> | null;
  calendarFilters: CalendarFilters;
  filters: StaffFilters;
  page: number;
  branches: Page<Branch> | null;
  selectedId: string;
  selected: ApiResult<AppointmentDetail> | null;
}

const unloaded = { status: 200, data: null, message: "", uncertain: false };

export type IntakeViewComponent =
  | { view: "calendar"; Component: typeof IntakeCalendar }
  | { view: "list"; Component: typeof IntakeWorklist }
  | { view: "queue"; Component: typeof IntakeAgentQueue };

export function useIntakeWorkspace(
  initial: IntakeWorkspaceSnapshot,
  initialView: IntakeViewComponent,
  agent: boolean,
) {
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
  const drawer = useStaffAppointmentDrawer(initial.selectedId, initial.selected, agent);
  const [content, setContent] = useState(initialView);
  const view = content.view;
  const [pending, setPending] = useState<IntakeView | null>(null);
  const attempt = useRef<symbol | null>(null);
  const confirmedHref = useRef("");
  const toast = useToast();
  const interaction = useGuardedAction();
  const busy = calendar.busy || worklist.busy;
  const worklistHref = `/dashboard/intake?${staffViewQuery(worklist.active.filters, worklist.active.page)}`;
  const destinations = {
    queue: view === "queue" ? worklistHref : queueViewHref(),
    calendar:
      view === "calendar"
        ? calendarHref(calendar.active)
        : calendarViewHref(worklist.active.filters),
    list: view === "list" ? worklistHref : tableViewHref(calendar.active),
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

  async function prepareView(nextView: IntakeView): Promise<IntakeViewComponent> {
    if (nextView === content.view) {
      return content;
    }
    if (nextView === "calendar") {
      const { IntakeCalendar } = await import("@/components/staff/IntakeCalendar");
      return { view: "calendar", Component: IntakeCalendar };
    }
    if (nextView === "queue") {
      const { IntakeAgentQueue } = await import("@/components/staff/IntakeAgentQueue");
      return { view: "queue", Component: IntakeAgentQueue };
    }
    const { IntakeWorklist } = await import("@/components/staff/IntakeWorklist");
    return { view: "list", Component: IntakeWorklist };
  }

  function prefetchView(nextView: IntakeView) {
    void prepareView(nextView).catch(() => {});
  }

  function cancelNavigation() {
    attempt.current = null;
    calendar.cancel();
    worklist.cancel();
    setPending(null);
  }

  function onConfirmed(detail: AppointmentDetail) {
    if (view === "calendar") {
      calendar.cancel();
      void calendar.load();
      return;
    }
    worklist.cancel();
    const filters = worklist.active.filters;
    const nextFilters =
      view === "queue" ? { ...filters, queue: appointmentQueue(detail) } : filters;
    void worklist.load(nextFilters, view === "queue" ? 1 : worklist.active.page);
  }

  async function navigate(destination: string, history: "push" | "replace" = "push") {
    if (drawer.id && !agent) {
      return;
    }
    interaction.request(() => {
      void navigateConfirmed(destination, history);
    });
  }

  async function navigateConfirmed(destination: string, history: "push" | "replace") {
    calendar.cancel();
    worklist.cancel();
    const currentAttempt = Symbol();
    attempt.current = currentAttempt;
    const url = new URL(destination, window.location.origin);
    const params = Object.fromEntries(url.searchParams);
    const nextView = intakeView(params.view, agent);
    setPending(nextView);
    const nextCalendar = calendarParams(params);
    const nextWorklist =
      nextView === "queue" ? intakeQueueParams(params) : staffParams({ ...params, queue: "" });
    try {
      const [success, prepared] = await Promise.all([
        nextView === "calendar"
          ? calendar.load(nextCalendar, false)
          : worklist.load(nextWorklist.filters, nextWorklist.page, false),
        prepareView(nextView),
      ]);
      if (attempt.current !== currentAttempt || !success) {
        return;
      }
      drawer.clearSelection();
      const nextHref =
        nextView === "calendar"
          ? calendarHref(nextCalendar)
          : `/dashboard/intake?${staffViewQuery(nextWorklist.filters, nextWorklist.page)}`;
      window.history[history === "push" ? "pushState" : "replaceState"](null, "", nextHref);
      setContent(prepared);
    } catch {
      if (attempt.current === currentAttempt) {
        calendar.cancel();
        worklist.cancel();
        toast.error("The view could not be opened. The last confirmed view remains shown.");
      }
    } finally {
      if (attempt.current === currentAttempt) {
        attempt.current = null;
        setPending(null);
      }
    }
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
    cancelNavigation();
  });

  useEffect(() => {
    window.addEventListener("popstate", restore);
    window.addEventListener("keydown", cancel);
    return () => {
      window.removeEventListener("popstate", restore);
      window.removeEventListener("keydown", cancel);
    };
  }, []);

  return {
    view,
    content,
    pending,
    busy,
    calendar,
    worklist,
    drawer,
    destinations,
    navigate,
    prefetchView,
    cancelNavigation,
    onConfirmed,
  };
}

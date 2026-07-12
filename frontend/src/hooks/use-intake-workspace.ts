"use client";

import { useEffect, useEffectEvent, useRef } from "react";
import { useStaffWorklist } from "./use-staff-worklist";
import { useStaffAppointmentDrawer } from "./use-staff-appointment-drawer";
import { useGuardedAction } from "./use-guarded-action";
import type { IntakeWorklist } from "@/components/staff/IntakeWorklist";
import type { IntakeAgentQueue } from "@/components/staff/IntakeAgentQueue";
import {
  appointmentQueue,
  intakeQueueParams,
  staffParams,
  staffViewQuery,
} from "@/lib/staff/query";
import type { ApiResult, Page } from "@/lib/api";
import type { Branch } from "@/lib/branches";
import type { AppointmentDetail, StaffFilters, WorklistPage } from "@/lib/staff/contracts";
import { writeAddress } from "@/lib/browser-url";

export interface IntakeWorkspaceSnapshot {
  worklist: ApiResult<WorklistPage>;
  filters: StaffFilters;
  page: number;
  branches: Page<Branch> | null;
  selectedId: string;
  selected: ApiResult<AppointmentDetail> | null;
}

export type IntakeViewComponent =
  | { view: "list"; Component: typeof IntakeWorklist }
  | { view: "queue"; Component: typeof IntakeAgentQueue };

export function useIntakeWorkspace(initial: IntakeWorkspaceSnapshot, agent: boolean) {
  const worklist = useStaffWorklist(
    initial.worklist,
    initial.filters,
    initial.page,
    initial.branches,
  );
  const drawer = useStaffAppointmentDrawer(initial.selectedId, initial.selected, agent);
  const interaction = useGuardedAction();
  const confirmedHref = useRef("");
  const href = `/dashboard/intake?${staffViewQuery(worklist.active.filters, worklist.active.page)}`;

  useEffect(() => {
    const url = new URL(href, window.location.origin);
    if (drawer.id) {
      url.searchParams.set("appointment", drawer.id);
    }
    confirmedHref.current = `${url.pathname}${url.search}`;
  }, [href, drawer.id]);

  const restore = useEffectEvent(() => {
    if (window.location.pathname !== "/dashboard/intake") {
      return;
    }
    const destination = new URL(window.location.href);
    writeAddress(confirmedHref.current);
    interaction.request(() => {
      const params = Object.fromEntries(destination.searchParams);
      const next = agent ? intakeQueueParams(params) : staffParams({ ...params, queue: "" });
      worklist.cancel();
      void worklist.load(next.filters, next.page);
    });
  });

  useEffect(() => {
    window.addEventListener("popstate", restore);
    return () => window.removeEventListener("popstate", restore);
  }, []);

  function onConfirmed(detail: AppointmentDetail) {
    worklist.cancel();
    const filters = worklist.active.filters;
    const next = agent ? { ...filters, queue: appointmentQueue(detail) } : filters;
    void worklist.load(next, agent ? 1 : worklist.active.page);
  }

  return { worklist, drawer, onConfirmed };
}

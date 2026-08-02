"use client";

import { useIntakeHistory } from "./use-intake-history";
import type { IntakeWorklist } from "@/components/staff/IntakeWorklist";
import type { IntakeAgentQueue } from "@/components/staff/IntakeAgentQueue";
import { appointmentQueue } from "@/lib/staff/query";
import type { ApiResult, Page } from "@/lib/api";
import type { Branch } from "@/lib/branches";
import type { AppointmentDetail, StaffFilters, WorklistPage } from "@/lib/staff/contracts";

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
  const { worklist, drawer, busy } = useIntakeHistory(initial, agent);

  function onConfirmed(detail: AppointmentDetail) {
    worklist.cancel();
    const filters = worklist.active.filters;
    const next = agent ? { ...filters, queue: appointmentQueue(detail) } : filters;
    void worklist.load(next, agent ? 1 : worklist.active.page);
  }

  return {
    worklist,
    drawer,
    onConfirmed,
    historyBusy: busy,
  };
}

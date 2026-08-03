"use client";

import { useEffect } from "react";
import { useToast } from "@/components/ui/Toast";
import { useStaffClientEntry } from "./use-staff-client-entry";
import { privateApiFetch } from "@/lib/api-browser";
import type { CurrentActor } from "@/lib/auth/actor";
import { isUuid } from "@/lib/auth/contracts";
import type { SearchParams } from "@/lib/query";
import { parseDetail, parseWorklistPage } from "@/lib/staff/parse";
import {
  appointmentQueue,
  intakeQueueParams,
  parseIntakeQueue,
  staffParams,
  staffQuery,
} from "@/lib/staff/query";
import type { IntakeViewComponent, IntakeWorkspaceSnapshot } from "./use-intake-workspace";
import { readBranchChoices } from "@/lib/branch-choices";

interface IntakeEntry {
  snapshot: IntakeWorkspaceSnapshot;
  view: IntakeViewComponent;
}

export function useIntakeClientEntry(actor: CurrentActor, parameters: SearchParams) {
  const { error } = useToast();
  const agent = actor.role === "SalesAgent";
  const result = useStaffClientEntry<IntakeEntry>(async (signal) => {
    const { filters, page } = agent
      ? intakeQueueParams(parameters)
      : staffParams({ ...parameters, queue: "" });
    const selectedId = isUuid(parameters.appointment) ? parameters.appointment : "";
    const selectedRead = selectedId
      ? privateApiFetch(
          `/api/staff/intake/appointments/${selectedId}`,
          (value) => {
            const detail = parseDetail(value);
            return detail?.id === selectedId ? detail : null;
          },
          { signal },
        ).catch(() => null)
      : Promise.resolve(null);
    const explicitQueue = Boolean(parseIntakeQueue(parameters.queue));
    if (agent && selectedId && !explicitQueue) {
      const selected = await selectedRead;
      if (selected?.data) {
        filters.queue = appointmentQueue(selected.data);
      }
    }
    const [worklist, branches, selected, view] = await Promise.all([
      privateApiFetch(
        `/api/staff/intake/appointments?${staffQuery(filters, page)}`,
        parseWorklistPage,
        { signal },
      ),
      actor.role === "GeneralManager" ? readBranchChoices(1, signal, 25).catch(() => null) : null,
      selectedRead,
      prepareView(agent),
    ]);
    if (
      worklist.data &&
      (worklist.data.page !== page || worklist.data.pageSize !== filters.pageSize)
    ) {
      throw new Error("Appointments unavailable");
    }
    return {
      view,
      snapshot: {
        worklist,
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

async function prepareView(agent: boolean): Promise<IntakeViewComponent> {
  if (agent) {
    const { IntakeAgentQueue } = await import("@/components/staff/IntakeAgentQueue");
    return { view: "queue", Component: IntakeAgentQueue };
  }
  const { IntakeWorklist } = await import("@/components/staff/IntakeWorklist");
  return { view: "list", Component: IntakeWorklist };
}

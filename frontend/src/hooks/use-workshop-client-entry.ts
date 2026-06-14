"use client";

import type { ComponentProps } from "react";
import type { WorkshopQueue } from "@/components/inspection/WorkshopQueue";
import type { TechnicianWorkspace } from "@/components/inspection/TechnicianWorkspace";
import { useStaffClientEntry } from "./use-staff-client-entry";
import { unavailableResult } from "@/lib/api";
import type { CurrentActor } from "@/lib/auth/actor";
import type { SearchParams } from "@/lib/query";
import { inspectionFetch } from "@/lib/inspection/browser";
import { parseEditor, type EditorSnapshot } from "@/lib/inspection/editor-snapshot";
import { parseWorkshopJobs, parseWorkshopPage } from "@/lib/inspection/parse-workshop";
import { readRepairReview, type RepairReview } from "@/lib/inspection/repair-review";
import {
  queueParams,
  queueQuery,
  technicianQueueParams,
  technicianQueuePaging,
  technicianQueueQuery,
  technicianQueueView,
  workshopSelection,
} from "@/lib/inspection/query";
import { readBranchChoices } from "@/lib/branch-choices";

type WorkshopEntry =
  | {
      kind: "manager";
      Workspace: typeof WorkshopQueue;
      props: Omit<ComponentProps<typeof WorkshopQueue>, "actor">;
    }
  | {
      kind: "technician";
      Workspace: typeof TechnicianWorkspace;
      props: Omit<ComponentProps<typeof TechnicianWorkspace>, "actor">;
    };

async function readSelection(parameters: SearchParams, signal: AbortSignal) {
  const selected = workshopSelection(parameters);
  const [inspection, review] = await Promise.all([
    selected.id
      ? inspectionFetch(
          `drafts/${selected.id}/workspace`,
          (value) => {
            const snapshot = parseEditor(value);
            return snapshot?.draft.id === selected.id ? snapshot : null;
          },
          { signal },
        ).catch(() =>
          unavailableResult<EditorSnapshot>(
            "The inspection could not be loaded. Use refresh to retry.",
          ),
        )
      : null,
    selected.id && selected.request
      ? readRepairReview(selected.id, selected.request, inspectionFetch, signal)
          .then((data) => ({ status: 200, data, message: "", uncertain: false }))
          .catch(() =>
            unavailableResult<RepairReview>(
              "The selected repair request could not be loaded. Use refresh to retry.",
            ),
          )
      : null,
  ]);
  return {
    selectedId: selected.id,
    selectedInspection: inspection,
    selectedRequest: selected.request,
    selectedSection: selected.section,
    selectedReview:
      selected.request && review ? { selection: selected.request, result: review } : null,
  };
}

async function readManager(actor: CurrentActor, parameters: SearchParams, signal: AbortSignal) {
  const { filters, page } = queueParams(parameters, "workshop");
  const [initial, branches, { WorkshopQueue: Workspace }] = await Promise.all([
    inspectionFetch(`workshop?${queueQuery(filters, page)}`, parseWorkshopPage, { signal }),
    actor.role === "GeneralManager" ? readBranchChoices(1, signal).catch(() => null) : null,
    import("@/components/inspection/WorkshopQueue"),
  ]);
  if (initial.data && (initial.data.page !== page || initial.data.pageSize !== filters.pageSize)) {
    throw new Error("Workshop queue unavailable");
  }
  return { Workspace, initial, filters, page, branches: branches?.data ?? null };
}

async function readTechnician(parameters: SearchParams, signal: AbortSignal) {
  const entries = await Promise.all(
    (["MyWork", "Waiting", "Claimable"] as const).map(async (task) => {
      const search = technicianQueueParams(parameters, task);
      const { page, pageSize } = technicianQueuePaging(parameters, task);
      const initial = await inspectionFetch(
        `workshop/jobs?${technicianQueueQuery(task, search, page, pageSize)}`,
        parseWorkshopJobs,
        { signal },
      );
      if (initial.data && (initial.data.page !== page || initial.data.pageSize !== pageSize)) {
        throw new Error("Workshop jobs unavailable");
      }
      return { task, search, initial };
    }),
  );
  return {
    MyWork: entries[0],
    Waiting: entries[1],
    Claimable: entries[2],
  };
}

export function useWorkshopClientEntry(actor: CurrentActor, parameters: SearchParams) {
  return useStaffClientEntry<WorkshopEntry>(async (signal) => {
    let next: WorkshopEntry;
    if (actor.role === "WorkshopTechnician") {
      const [queues, selected, { TechnicianWorkspace: Workspace }] = await Promise.all([
        readTechnician(parameters, signal),
        readSelection(parameters, signal),
        import("@/components/inspection/TechnicianWorkspace"),
      ]);
      next = {
        kind: "technician",
        Workspace,
        props: { queues, initialView: technicianQueueView(parameters), ...selected },
      };
    } else {
      const [{ Workspace, ...queue }, selected] = await Promise.all([
        readManager(actor, parameters, signal),
        readSelection(parameters, signal),
      ]);
      next = { kind: "manager", Workspace, props: { ...queue, ...selected } };
    }
    signal.throwIfAborted();
    return next;
  }, "Workshop could not be loaded. Retry to open the workspace.");
}

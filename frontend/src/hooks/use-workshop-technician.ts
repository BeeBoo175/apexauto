"use client";

import { useState } from "react";
import { useToast } from "@/components/ui/Toast";
import type { ApiResult } from "@/lib/api";
import type { EditorSnapshot } from "@/lib/inspection/editor-snapshot";
import type { RepairSelection, WorkshopSection } from "@/lib/inspection/query";
import {
  technicianTasks,
  type TechnicianTask,
  type WorkshopJobs,
} from "@/lib/inspection/workshop-contracts";
import type {
  TechnicianQueueState,
  TechnicianQueues,
  WorkshopSelectionPath,
  WorkshopState,
} from "@/lib/inspection/workshop-location";
import { queueParams } from "@/lib/inspection/query";
import { useGuardedAction } from "./use-guarded-action";
import { useInspectionDrawer } from "./use-inspection-drawer";
import { useTechnicianQueue } from "./use-technician-queue";
import {
  useWorkshopAddress,
  useWorkshopSelectedRecord,
  workshopListKey,
  type WorkshopTarget,
} from "./use-workshop-address";

export type TechnicianQueueEntry = TechnicianQueueState & {
  initial: ApiResult<WorkshopJobs>;
};

export interface WorkshopTechnicianEntry {
  queues: Record<TechnicianTask, TechnicianQueueEntry>;
  initialView: TechnicianTask;
  selectedId: string;
  selectedInspection: ApiResult<EditorSnapshot> | null;
  selectedRequest: RepairSelection | null;
  selectedSection: WorkshopSection;
  selectedCategory: string;
}

function queueStates(entry: WorkshopTechnicianEntry): TechnicianQueues {
  const state = ({ search, page, pageSize }: TechnicianQueueEntry) => ({ search, page, pageSize });
  return {
    MyWork: state(entry.queues.MyWork),
    Waiting: state(entry.queues.Waiting),
    Claimable: state(entry.queues.Claimable),
  };
}

export function useWorkshopTechnician(entry: WorkshopTechnicianEntry) {
  const toast = useToast();
  const guard = useGuardedAction();
  const [view, setView] = useState(entry.initialView);
  const initial: WorkshopState = {
    id: entry.selectedId,
    section: entry.selectedSection,
    request: entry.selectedRequest,
    category: entry.selectedCategory,
    filters: queueParams({}, "workshop").filters,
    page: 1,
    view: entry.initialView,
    queues: queueStates(entry),
  };
  const address = useWorkshopAddress(initial, true, synchronize);

  function queueLocation(
    task: TechnicianTask,
    read: TechnicianQueueState,
    mode: false | "push" | "replace",
  ) {
    const { search, page, pageSize } = read;
    const queues = { ...address.confirmed.current.queues, [task]: { search, page, pageSize } };
    address.list({ queues }, mode);
  }

  const ready = useTechnicianQueue(
    "MyWork",
    entry.queues.MyWork.initial,
    entry.queues.MyWork.search,
    queueLocation,
  );
  const waiting = useTechnicianQueue(
    "Waiting",
    entry.queues.Waiting.initial,
    entry.queues.Waiting.search,
    queueLocation,
  );
  const claimable = useTechnicianQueue(
    "Claimable",
    entry.queues.Claimable.initial,
    entry.queues.Claimable.search,
    queueLocation,
  );
  const worklists = { MyWork: ready, Waiting: waiting, Claimable: claimable };

  async function refresh(includeClaims = true) {
    const results = await Promise.all([
      ready.refresh(false),
      waiting.refresh(false),
      ...(includeClaims ? [claimable.refresh(false)] : []),
    ]);
    if (results.some((result) => result === false)) {
      toast.error(
        "Workshop jobs could not all be refreshed. The last confirmed worklists remain shown.",
      );
    }
  }

  const drawer = useInspectionDrawer(
    entry.selectedId,
    entry.selectedInspection,
    () => void refresh(),
    (selection, mode) => address.select(selection, mode),
    entry.selectedRequest,
    entry.selectedSection,
    entry.selectedCategory,
  );
  useWorkshopSelectedRecord(drawer.id);

  function showView(value: TechnicianTask, mode: false | "push" | "replace" = "push") {
    setView(value);
    address.list({ view: value }, mode);
  }

  function visitRecord(target: WorkshopSelectionPath) {
    if (target.id) {
      void drawer.visit(target);
    } else if (drawer.id || drawer.pendingId) {
      drawer.closeConfirmed(false);
    }
  }

  function visitQueues(target: WorkshopTarget) {
    for (const task of technicianTasks) {
      const wanted = target.queues[task];
      const shown = worklists[task];
      const same =
        wanted.search === shown.search &&
        wanted.page === shown.page &&
        wanted.pageSize === shown.pageSize;
      if (!same) {
        void shown.visit(wanted);
      }
    }
  }

  function synchronize(target: WorkshopTarget, previous: WorkshopTarget | null) {
    setView(target.view);
    address.list({ view: target.view, queues: target.queues }, false);
    const key = workshopListKey(target, true);
    if (previous && previous.id === target.id && workshopListKey(previous, true) === key) {
      void drawer.visit(target);
      return;
    }
    visitQueues(target);
    visitRecord(target);
  }

  // Refresh follows the address: after a failed Back/Forward it reads the requested destination.
  function refreshDestination() {
    const target = address.destination();
    if (!target || (target.id === drawer.id && target.section === drawer.section)) {
      return refresh();
    }
    address.attempted(target);
    guard.request(() => {
      visitQueues(target);
      visitRecord(target);
    });
    return Promise.resolve();
  }

  return { view, showView, worklists, drawer, refresh, refreshDestination };
}

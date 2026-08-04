"use client";

import type { ApiResult, Page } from "@/lib/api";
import type { Branch } from "@/lib/branches";
import type { QueueFilters } from "@/lib/inspection/contracts";
import type { EditorSnapshot } from "@/lib/inspection/editor-snapshot";
import type { RepairSelection, WorkshopSection } from "@/lib/inspection/query";
import { parseWorkshopPage } from "@/lib/inspection/parse-workshop";
import {
  idleTechnicianQueues,
  type WorkshopState,
  type WorkshopSelectionPath,
} from "@/lib/inspection/workshop-location";
import type { WorkshopRow } from "@/lib/inspection/workshop-contracts";
import { useGuardedAction } from "./use-guarded-action";
import { useInspectionDrawer } from "./use-inspection-drawer";
import { useInspectionQueue } from "./use-inspection-queue";
import {
  useWorkshopAddress,
  useWorkshopSelectedRecord,
  workshopListKey,
  type WorkshopTarget,
} from "./use-workshop-address";

export interface WorkshopManagerEntry {
  initial: ApiResult<Page<WorkshopRow>>;
  filters: QueueFilters;
  page: number;
  branches: Page<Branch> | null;
  selectedId: string;
  selectedInspection: ApiResult<EditorSnapshot> | null;
  selectedRequest: RepairSelection | null;
  selectedSection: WorkshopSection;
  selectedCategory: string;
}

export function useWorkshopManager(entry: WorkshopManagerEntry) {
  const guard = useGuardedAction();
  const initial: WorkshopState = {
    id: entry.selectedId,
    section: entry.selectedSection,
    request: entry.selectedRequest,
    category: entry.selectedCategory,
    filters: entry.filters,
    page: entry.page,
    view: "MyWork",
    queues: idleTechnicianQueues,
  };
  const address = useWorkshopAddress(initial, false, synchronize);
  const queue = useInspectionQueue(
    "workshop",
    entry.initial,
    entry.filters,
    entry.page,
    entry.branches,
    parseWorkshopPage,
    (filters, page, mode) => address.list({ filters, page }, mode),
  );
  const drawer = useInspectionDrawer(
    entry.selectedId,
    entry.selectedInspection,
    () => void queue.refresh(),
    (selection, mode) => address.select(selection, mode),
    entry.selectedRequest,
    entry.selectedSection,
    entry.selectedCategory,
  );

  useWorkshopSelectedRecord(drawer.id);

  function synchronize(target: WorkshopTarget, previous: WorkshopTarget | null) {
    const key = workshopListKey(target, false);
    if (previous && previous.id === target.id && workshopListKey(previous, false) === key) {
      void drawer.visit(target);
      return;
    }
    queue.cancel();
    if (key !== workshopListKey(queueState(), false)) {
      void queue.load(target.filters, target.page, undefined, false, false, true);
    }
    visitRecord(target);
  }

  function queueState(): WorkshopState {
    return { ...initial, filters: queue.active.filters, page: queue.active.page };
  }

  function visitRecord(target: WorkshopSelectionPath) {
    if (target.id) {
      void drawer.visit(target);
    } else if (drawer.id || drawer.pendingId) {
      drawer.closeConfirmed(false);
    }
  }

  // Refresh follows the address: after a failed Back/Forward it reads the requested destination.
  function refresh() {
    const target = address.destination();
    if (!target || (target.id === drawer.id && target.section === drawer.section)) {
      return queue.refresh();
    }
    address.attempted(target);
    guard.request(() => {
      queue.cancel();
      visitRecord(target);
      void queue.load(target.filters, target.page, undefined, true, false, true);
    });
    return Promise.resolve(false);
  }

  return { queue, drawer, guard, refresh };
}

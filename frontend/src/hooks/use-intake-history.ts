"use client";

import { useContext, useEffectEvent, useLayoutEffect, useRef, useState } from "react";
import { StaffLocalHistoryContext } from "@/components/staff/StaffClientEntryBoundary";
import { useGuardedAction } from "./use-guarded-action";
import { useStaffWorklist } from "./use-staff-worklist";
import { useStaffAppointmentDrawer } from "./use-staff-appointment-drawer";
import type { IntakeWorkspaceSnapshot } from "./use-intake-workspace";
import { useToast } from "@/components/ui/Toast";
import { privateApiFetch } from "@/lib/api-browser";
import { writeAddress } from "@/lib/browser-url";
import { intakeLocation, intakeLocationId } from "@/lib/staff/intake-location";
import { intakeQueueParams, staffParams, staffQuery } from "@/lib/staff/query";
import { parseDetail, parseWorklistPage } from "@/lib/staff/parse";
import type { StaffFilters } from "@/lib/staff/contracts";
import { useLoadingProgress } from "@/components/ui/LoadingProgress";

interface HistoryEntry {
  index: number;
  href: string;
  state: Record<string, unknown>;
}

export function useIntakeHistory(initial: IntakeWorkspaceSnapshot, agent: boolean) {
  const localHistoryRef = useContext(StaffLocalHistoryContext);
  const guard = useGuardedAction();
  const toast = useToast();
  const owner = useRef(crypto.randomUUID());
  const confirmed = useRef<HistoryEntry | null>(null);
  const entries = useRef(new Map<number, HistoryEntry>());
  const movement = useRef<{ kind: "return" | "commit"; target: HistoryEntry } | null>(null);
  const restoring = useRef(false);
  const active = useRef(true);
  const controller = useRef<AbortController | null>(null);
  const [busy, setBusy] = useState(false);
  const worklist = useStaffWorklist(
    initial.worklist,
    initial.filters,
    initial.page,
    initial.branches,
    list,
  );
  const drawer = useStaffAppointmentDrawer(initial.selectedId, initial.selected, true, select);
  useLoadingProgress("Opening intake view", busy);

  function address(
    id: string,
    mode: "push" | "replace" = "push",
    filters = worklist.active.filters,
    page = worklist.active.page,
  ) {
    if (restoring.current) {
      return;
    }
    const href = intakeLocation(filters, page, id);
    const previous = confirmed.current;
    if (previous?.href === href) {
      return;
    }
    const index = (previous?.index ?? 0) + (mode === "push" ? 1 : 0);
    const state = { intakeWorkspace: owner.current, intakeIndex: index };
    writeAddress(href, state, mode);
    const entry = { index, href, state };
    confirmed.current = entry;
    entries.current.set(index, entry);
  }

  function select(id: string) {
    address(id, id ? "push" : "replace");
  }

  function list(filters: StaffFilters, page: number) {
    const changed =
      filters.queue !== worklist.active.filters.queue ||
      filters.status !== worklist.active.filters.status;
    address(drawer.id, changed ? "push" : "replace", filters, page);
  }

  function close() {
    guard.request(() => {
      if (drawer.pendingId && !drawer.id) {
        drawer.clearSelection();
        return;
      }
      const current = confirmed.current;
      const previous = current && entries.current.get(current.index - 1);
      if (
        previous &&
        intakeLocationId(new URL(previous.href, window.location.origin).pathname) === ""
      ) {
        window.history.back();
        return;
      }
      drawer.clearSelection();
    }, "Discard and close");
  }

  async function restore(target: HistoryEntry) {
    const destination = new URL(target.href, window.location.origin);
    const id = intakeLocationId(destination.pathname);
    if (id === null || restoring.current) {
      return;
    }
    const parameters = Object.fromEntries(destination.searchParams);
    const next = agent ? intakeQueueParams(parameters) : staffParams({ ...parameters, queue: "" });
    const listChanged =
      staffQuery(next.filters, next.page) !==
      staffQuery(worklist.active.filters, worklist.active.page);
    restoring.current = true;
    setBusy(true);
    const attempt = new AbortController();
    controller.current = attempt;
    try {
      // Prepare the whole destination before either owner replaces confirmed content.
      const [rows, detail] = await Promise.all([
        listChanged
          ? privateApiFetch(
              `/api/staff/intake/appointments?${staffQuery(next.filters, next.page)}`,
              parseWorklistPage,
              { signal: attempt.signal },
            )
          : null,
        id && id !== drawer.id
          ? privateApiFetch(`/api/staff/intake/appointments/${id}`, parseDetail, {
              signal: attempt.signal,
            })
          : null,
      ]);
      if (!active.current) {
        return;
      }
      if (
        listChanged &&
        (rows?.status !== 200 ||
          rows.data?.page !== next.page ||
          rows.data.pageSize !== next.filters.pageSize)
      ) {
        throw new Error("Appointments unavailable");
      }
      if (id && id !== drawer.id && (detail?.status !== 200 || detail.data?.id !== id)) {
        throw new Error("Appointment unavailable");
      }
      if (listChanged) {
        worklist.cancel();
        await worklist.load(next.filters, next.page, false);
      }
      if (id !== drawer.id) {
        if (id) {
          await drawer.load(id);
        } else {
          drawer.clearSelection();
        }
      }
      const current = confirmed.current;
      if (current && active.current) {
        movement.current = { kind: "commit", target };
        window.history.go(target.index - current.index);
      }
    } catch {
      if (active.current) {
        toast.error("The appointment view could not be opened. Your confirmed view remains shown.");
      }
    } finally {
      restoring.current = false;
      controller.current = null;
      if (active.current && movement.current?.kind !== "commit") {
        setBusy(false);
      }
    }
  }

  const traverse = useEffectEvent((event: PopStateEvent) => {
    const state = event.state;
    const current = confirmed.current;
    if (
      !current ||
      state?.intakeWorkspace !== owner.current ||
      !Number.isInteger(state.intakeIndex)
    ) {
      return false;
    }
    const target = entries.current.get(state.intakeIndex);
    if (!target || intakeLocationId(window.location.pathname) === null) {
      return false;
    }
    if (restoring.current) {
      if (target.index !== current.index) {
        window.history.go(current.index - target.index);
      }
      return true;
    }
    const moving = movement.current;
    if (moving) {
      const expected = moving.kind === "commit" ? moving.target.index : current.index;
      if (target.index !== expected) {
        window.history.go(expected - target.index);
        return true;
      }
      movement.current = null;
      if (moving.kind === "commit") {
        confirmed.current = moving.target;
        writeAddress(moving.target.href, moving.target.state);
        setBusy(false);
      } else {
        writeAddress(current.href, current.state);
        const targetPath = new URL(moving.target.href, window.location.origin).pathname;
        if (intakeLocationId(targetPath) === drawer.id) {
          void restore(moving.target);
        } else {
          guard.request(() => void restore(moving.target));
        }
      }
      return true;
    }
    if (target.index !== current.index) {
      movement.current = { kind: "return", target };
      window.history.go(current.index - target.index);
    }
    return true;
  });

  const initialize = useEffectEvent(() => address(drawer.id, "replace"));

  useLayoutEffect(() => {
    active.current = true;
    initialize();
    if (localHistoryRef) {
      localHistoryRef.current = traverse;
      localHistoryRef.pathname = "/dashboard/intake";
    }
    return () => {
      active.current = false;
      controller.current?.abort();
      if (localHistoryRef) {
        localHistoryRef.current = null;
        localHistoryRef.pathname = undefined;
      }
    };
  }, [localHistoryRef]);

  return { worklist, drawer: { ...drawer, close }, busy };
}

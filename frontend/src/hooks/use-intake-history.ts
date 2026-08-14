"use client";

import { useContext, useEffect, useEffectEvent, useLayoutEffect, useRef, useState } from "react";
import { usePathname, useSearchParams } from "next/navigation";
import { StaffLocalHistoryContext } from "@/components/staff/StaffClientEntryBoundary";
import { useGuardedAction } from "./use-guarded-action";
import { useStaffWorklist } from "./use-staff-worklist";
import { useStaffAppointmentDrawer } from "./use-staff-appointment-drawer";
import type { IntakeWorkspaceSnapshot } from "./use-intake-workspace";
import { writeAddress } from "@/lib/browser-url";
import { intakeLocation, intakeLocationId, isIntakeCreating } from "@/lib/staff/intake-location";
import { intakeQueueParams, managerQueueParams, staffQuery } from "@/lib/staff/query";
import type { StaffFilters } from "@/lib/staff/contracts";

function parseIntakeUrl(pathname: string, search: string, agent: boolean) {
  const pathId = intakeLocationId(pathname);
  if (pathId === null) {
    return null;
  }
  const parameters = Object.fromEntries(new URLSearchParams(search));
  const { filters, page } = agent ? intakeQueueParams(parameters) : managerQueueParams(parameters);
  const creating = isIntakeCreating(search);
  const href = intakeLocation(filters, page, pathId, creating);
  return { id: pathId, filters, page, creating, href };
}

// The record drawer plus the new-intake drawer, whose open state lives in the address.
export type IntakeDrawer = ReturnType<typeof useStaffAppointmentDrawer> & {
  creating: boolean;
  openCreate: () => void;
  closeCreate: () => void;
};

export function useIntakeHistory(initial: IntakeWorkspaceSnapshot, agent: boolean) {
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const localHistoryRef = useContext(StaffLocalHistoryContext);
  const guard = useGuardedAction();
  // A reload or shared address with ?create=intake reopens the new-intake drawer.
  const [initialCreating] = useState(
    () => typeof window !== "undefined" && isIntakeCreating(window.location.search),
  );
  const [creating, setCreating] = useState(initialCreating);
  const initialHref = intakeLocation(
    initial.filters,
    initial.page,
    initial.selectedId,
    initialCreating,
  );
  const lastAttemptedHref = useRef(initialHref);
  const confirmed = useRef({
    filters: initial.filters,
    page: initial.page,
    id: initial.selectedId,
  });
  const worklist = useStaffWorklist(
    initial.worklist,
    initial.filters,
    initial.page,
    initial.branches,
    list,
  );
  const drawer = useStaffAppointmentDrawer(initial.selectedId, initial.selected, true, select);

  function commitAddress(href: string, state: unknown, mode: "push" | "replace") {
    lastAttemptedHref.current = href;
    const currentHref = `${window.location.pathname}${window.location.search}`;
    if (currentHref === href && mode === "push") {
      return;
    }
    writeAddress(href, state, mode);
  }

  // The new-intake drawer is applied like a record selection: an explicit address is pushed on open and on close.
  function openCreate() {
    const { filters, page, id } = confirmed.current;
    setCreating(true);
    commitAddress(intakeLocation(filters, page, id, true), null, "push");
  }

  function closeCreate() {
    const { filters, page, id } = confirmed.current;
    setCreating(false);
    commitAddress(intakeLocation(filters, page, id, false), null, "push");
  }

  function select(id: string, updateAddress = true) {
    confirmed.current.id = id;
    if (!updateAddress) {
      return;
    }
    const { filters, page } = confirmed.current;
    const href = intakeLocation(filters, page, id);
    commitAddress(href, null, "push");
  }

  function list(filters: StaffFilters, page: number, mode: false | "push" | "replace") {
    confirmed.current = { ...confirmed.current, filters, page };
    if (!mode) {
      return;
    }
    const href = intakeLocation(filters, page, confirmed.current.id);
    commitAddress(href, null, mode);
  }

  function replaceList(filters: StaffFilters, page: number) {
    return worklist.load(filters, page, "replace");
  }

  function close() {
    guard.request(() => {
      if (drawer.pendingId && !drawer.id) {
        drawer.clearSelection(false);
        return;
      }
      drawer.clearSelection(true);
    }, "Discard and close");
  }

  function refresh() {
    const target = parseIntakeUrl(window.location.pathname, window.location.search, agent);
    worklist.cancel();
    if (!target) {
      return worklist.load(worklist.active.filters, worklist.active.page, false);
    }
    lastAttemptedHref.current = target.href;
    if (target.id && target.id !== drawer.id) {
      void drawer.load(target.id, undefined, false);
    }
    return worklist.load(target.filters, target.page, false);
  }

  function retryTraversal() {
    const target = parseIntakeUrl(window.location.pathname, window.location.search, agent);
    if (!target || target.id === drawer.id) {
      return false;
    }
    guard.request(() => void refresh());
    return true;
  }

  const syncFromUrl = useEffectEvent(() => {
    const rawHref = `${window.location.pathname}${window.location.search}`;
    const target = parseIntakeUrl(window.location.pathname, window.location.search, agent);
    if (!target) {
      return;
    }
    if (rawHref !== target.href) {
      writeAddress(target.href, null, "replace");
    }
    if (target.href === lastAttemptedHref.current) {
      return;
    }
    lastAttemptedHref.current = target.href;
    setCreating(target.creating);
    worklist.cancel();
    const listChanged =
      staffQuery(target.filters, target.page) !==
      staffQuery(worklist.active.filters, worklist.active.page);
    if (listChanged) {
      void worklist.load(target.filters, target.page, false);
    }
    if (target.id !== drawer.id || Boolean(drawer.pendingId)) {
      if (target.id) {
        void drawer.load(target.id, undefined, false);
      } else {
        drawer.clearSelection(false);
      }
    }
  });

  useLayoutEffect(() => {
    lastAttemptedHref.current = initialHref;
    const currentHref = `${window.location.pathname}${window.location.search}`;
    if (currentHref !== initialHref) {
      writeAddress(initialHref, null, "replace");
    }
    if (localHistoryRef) {
      localHistoryRef.current = null;
      localHistoryRef.pathname = "/dashboard/intake";
    }
    return () => {
      if (localHistoryRef) {
        localHistoryRef.current = null;
        localHistoryRef.selectedId = undefined;
        localHistoryRef.pathname = undefined;
      }
    };
  }, [initialHref, localHistoryRef]);

  useLayoutEffect(() => {
    if (localHistoryRef) {
      localHistoryRef.selectedId = drawer.id;
    }
  }, [drawer.id, localHistoryRef]);

  const searchString = searchParams.toString();
  useEffect(() => {
    syncFromUrl();
  }, [pathname, searchString]);

  return {
    worklist,
    drawer: { ...drawer, close, creating, openCreate, closeCreate },
    replaceList,
    refresh,
    retryTraversal,
    busy: false,
  };
}

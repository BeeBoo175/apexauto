"use client";

import { useContext, useEffect, useEffectEvent, useLayoutEffect, useRef } from "react";
import { usePathname, useSearchParams } from "next/navigation";
import { StaffLocalHistoryContext } from "@/components/staff/StaffClientEntryBoundary";
import { writeAddress } from "@/lib/browser-url";
import {
  workshopHref,
  workshopLocation,
  workshopWorkspace,
  type WorkshopSelectionPath,
  type WorkshopState,
} from "@/lib/inspection/workshop-location";

export type WorkshopTarget = NonNullable<ReturnType<typeof workshopLocation>>;
type Mode = false | "push" | "replace";

// The query that identifies the list shown beside the selection, so section-only traversals can be told apart.
export function workshopListKey(state: WorkshopState, technician: boolean) {
  return workshopHref({ ...state, id: "", section: "inspection", request: null }, technician);
}

// Tells the shared navigation guard which record the workspace currently shows.
export function useWorkshopSelectedRecord(selectedId: string) {
  const localHistoryRef = useContext(StaffLocalHistoryContext);
  useLayoutEffect(() => {
    if (localHistoryRef) {
      Object.assign(localHistoryRef, { selectedId });
    }
  }, [localHistoryRef, selectedId]);
}

// Owns the address of the Workshop workspace for both roles: the path selects the record and section, the query
// holds the list. Applied actions push; normalization and post-write reconciliation replace.
export function useWorkshopAddress(
  initial: WorkshopState,
  technician: boolean,
  synchronize: (target: WorkshopTarget, previous: WorkshopTarget | null) => void,
) {
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const localHistoryRef = useContext(StaffLocalHistoryContext);
  const initialHref = workshopHref(initial, technician);
  const lastAttempted = useRef(initialHref);
  const confirmed = useRef(initial);

  function commit(mode: Mode) {
    if (!mode) {
      return;
    }
    const href = workshopHref(confirmed.current, technician);
    lastAttempted.current = href;
    if (`${window.location.pathname}${window.location.search}` !== href) {
      writeAddress(href, null, mode);
    }
  }

  function select(selection: WorkshopSelectionPath, mode: Mode) {
    confirmed.current = { ...confirmed.current, ...selection };
    commit(mode);
  }

  function list(update: Partial<WorkshopState>, mode: Mode) {
    confirmed.current = { ...confirmed.current, ...update };
    commit(mode);
  }

  function destination() {
    return workshopLocation(window.location.pathname, window.location.search, technician);
  }

  function attempted(target: WorkshopTarget) {
    lastAttempted.current = target.href;
  }

  const reconcile = useEffectEvent(() => {
    const target = destination();
    if (!target) {
      return;
    }
    if (`${window.location.pathname}${window.location.search}` !== target.href) {
      writeAddress(target.href, null, "replace");
    }
    if (target.href === lastAttempted.current) {
      return;
    }
    const previousUrl = new URL(lastAttempted.current, window.location.origin);
    const previous = workshopLocation(previousUrl.pathname, previousUrl.search, technician);
    lastAttempted.current = target.href;
    synchronize(target, previous);
  });

  useLayoutEffect(() => {
    lastAttempted.current = initialHref;
    if (`${window.location.pathname}${window.location.search}` !== initialHref) {
      writeAddress(initialHref, null, "replace");
    }
    if (localHistoryRef) {
      localHistoryRef.current = null;
      localHistoryRef.pathname = workshopWorkspace;
    }
    return () => {
      if (localHistoryRef) {
        localHistoryRef.current = null;
        localHistoryRef.pathname = undefined;
        localHistoryRef.selectedId = undefined;
      }
    };
  }, [initialHref, localHistoryRef]);

  const search = searchParams.toString();
  useEffect(() => reconcile(), [pathname, search]);

  return { confirmed, select, list, destination, attempted };
}

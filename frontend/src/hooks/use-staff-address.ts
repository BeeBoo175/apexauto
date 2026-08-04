"use client";

import { useContext, useEffect, useEffectEvent, useLayoutEffect, useRef } from "react";
import { usePathname, useSearchParams } from "next/navigation";
import { StaffLocalHistoryContext } from "@/components/staff/StaffClientEntryBoundary";
import { writeAddress } from "@/lib/browser-url";

export type AddressMode = false | "push" | "replace";

// Owns the address of one dashboard workspace. The state serializes to a path and query; applied actions push,
// while normalization and post-write reconciliation replace. A browser Back or Forward arrives as a changed
// pathname or search, is normalized to the canonical href and handed to `synchronize` with the previous target.
export function useStaffAddress<State, Target extends { href: string }>({
  workspace,
  initial,
  href,
  locate,
  synchronize,
}: {
  workspace: string;
  initial: State;
  href: (state: State) => string;
  locate: (pathname: string, search: string) => Target | null;
  synchronize: (target: Target, previous: Target | null) => void;
}) {
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const localHistoryRef = useContext(StaffLocalHistoryContext);
  const initialHref = href(initial);
  const lastAttempted = useRef(initialHref);
  const confirmed = useRef(initial);

  function current() {
    return `${window.location.pathname}${window.location.search}`;
  }

  function update(change: Partial<State>, mode: AddressMode) {
    confirmed.current = { ...confirmed.current, ...change };
    if (!mode) {
      return;
    }
    const target = href(confirmed.current);
    lastAttempted.current = target;
    if (current() !== target) {
      writeAddress(target, null, mode);
    }
  }

  function destination() {
    return locate(window.location.pathname, window.location.search);
  }

  function attempted(target: Target) {
    lastAttempted.current = target.href;
  }

  const reconcile = useEffectEvent(() => {
    const target = destination();
    if (!target) {
      return;
    }
    if (current() !== target.href) {
      writeAddress(target.href, null, "replace");
    }
    if (target.href === lastAttempted.current) {
      return;
    }
    const previousUrl = new URL(lastAttempted.current, window.location.origin);
    const previous = locate(previousUrl.pathname, previousUrl.search);
    lastAttempted.current = target.href;
    synchronize(target, previous);
  });

  useLayoutEffect(() => {
    lastAttempted.current = initialHref;
    if (current() !== initialHref) {
      writeAddress(initialHref, null, "replace");
    }
    if (localHistoryRef) {
      localHistoryRef.current = null;
      localHistoryRef.pathname = workspace;
    }
    return () => {
      if (localHistoryRef) {
        localHistoryRef.current = null;
        localHistoryRef.pathname = undefined;
        localHistoryRef.selectedId = undefined;
      }
    };
  }, [initialHref, localHistoryRef, workspace]);

  const search = searchParams.toString();
  useEffect(() => reconcile(), [pathname, search]);

  return { confirmed, update, destination, attempted };
}

// Tells the shared navigation guard which record the workspace currently shows.
export function useSelectedRecord(selectedId: string) {
  const localHistoryRef = useContext(StaffLocalHistoryContext);
  useLayoutEffect(() => {
    if (localHistoryRef) {
      Object.assign(localHistoryRef, { selectedId });
    }
  }, [localHistoryRef, selectedId]);
}

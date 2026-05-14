"use client";

import {
  useCallback,
  useContext,
  useEffectEvent,
  useLayoutEffect,
  useMemo,
  useOptimistic,
  useRef,
  useState,
  useSyncExternalStore,
  useTransition,
} from "react";
import { usePathname, useRouter } from "next/navigation";
import { createInteractionGuard, type InteractionBlock } from "@/lib/interaction-guard";
import {
  StaffEntryRequestsContext,
  StaffHistoryContext,
} from "@/components/staff/StaffClientEntryBoundary";

export function useStaffNavigation() {
  const pathname = usePathname();
  const router = useRouter();
  const entries = useContext(StaffEntryRequestsContext);
  const historyRef = useContext(StaffHistoryContext);
  const lockedRef = useRef(false);
  const [guard] = useState(createInteractionGuard);
  const guardState = useSyncExternalStore(guard.subscribe, guard.getSnapshot, guard.getSnapshot);
  const defaultOwner = useRef(Symbol("navigation"));
  const confirmed = useRef<{ href: string; state: unknown } | null>(null);
  const previousPath = useRef(pathname);
  const [entryPath, setEntryPath] = useState<string | null>(null);
  const [pending, startTransition] = useTransition();
  const [selectedPath, selectPath] = useOptimistic(pathname);

  const captureHistory = useCallback(() => {
    confirmed.current = { href: window.location.href, state: window.history.state };
  }, []);

  const setLocked = useCallback(
    (locked: boolean | InteractionBlock | null, owner = defaultOwner.current) => {
      const before = guard.isLocked();
      guard.setLocked(owner, locked);
      if (guard.isLocked() && !before) {
        confirmed.current = { href: window.location.href, state: window.history.state };
      }
      lockedRef.current = guard.isLocked();
    },
    [guard],
  );
  const operationLock = useMemo(() => ({ lockedRef, setLocked, guard }), [setLocked, guard]);

  const recordNavigation = useCallback(
    (href: string) => {
      entries?.cancel();
      const destination = new URL(href, window.location.origin).pathname;
      setEntryPath(destination === window.location.pathname ? null : destination);
      previousPath.current = destination;
    },
    [entries],
  );

  const protectOperation = useEffectEvent((event: PopStateEvent) => {
    if (!confirmed.current) {
      return;
    }
    if (lockedRef.current) {
      event.stopImmediatePropagation();
      const href = window.location.href;
      window.history.pushState(confirmed.current.state, "", confirmed.current.href);
      guard.request(() => {
        entries?.cancel();
        startTransition(() => {
          const destination = new URL(href).pathname;
          selectPath(destination);
          setEntryPath(destination === pathname ? null : destination);
          previousPath.current = destination;
          router.replace(href, { scroll: false });
        });
      });
      return;
    }
    if (previousPath.current !== window.location.pathname) {
      event.stopImmediatePropagation();
      entries?.cancel();
      const destination = window.location.pathname;
      const href = window.location.href;
      startTransition(() => {
        selectPath(destination);
        setEntryPath(destination);
        // Re-enter through the server gate without adding a new browser history entry.
        router.replace(href, { scroll: false });
      });
    }
    previousPath.current = window.location.pathname;
  });

  useLayoutEffect(() => {
    if (!historyRef) {
      return;
    }
    historyRef.current = protectOperation;
    return () => {
      historyRef.current = null;
    };
  }, [historyRef]);

  function navigate(event: { preventDefault: () => void }, href: string) {
    event.preventDefault();
    guard.request(() => {
      startTransition(() => {
        selectPath(href);
        if (pathname === href && window.location.pathname === href) {
          router.refresh();
          return;
        }
        router.push(href);
      });
    });
  }

  return {
    pathname,
    selectedPath,
    pending,
    lockedRef,
    operationLock,
    guardState,
    navigate,
    captureHistory,
    recordNavigation,
    entryPath,
  };
}

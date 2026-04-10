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
  useTransition,
} from "react";
import { usePathname, useRouter } from "next/navigation";
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
  const confirmed = useRef<{ href: string; state: unknown } | null>(null);
  const previousPath = useRef(pathname);
  const [entryPath, setEntryPath] = useState<string | null>(null);
  const [pending, startTransition] = useTransition();
  const [selectedPath, selectPath] = useOptimistic(pathname);

  const captureHistory = useCallback(() => {
    if (lockedRef.current) {
      return;
    }
    confirmed.current = { href: window.location.href, state: window.history.state };
  }, []);

  const setLocked = useCallback((locked: boolean) => {
    if (locked && !lockedRef.current) {
      confirmed.current = { href: window.location.href, state: window.history.state };
    }
    lockedRef.current = locked;
  }, []);
  const operationLock = useMemo(() => ({ lockedRef, setLocked }), [setLocked]);

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
      window.history.pushState(confirmed.current.state, "", confirmed.current.href);
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
    if (lockedRef.current) {
      return;
    }
    startTransition(() => {
      selectPath(href);
      if (pathname === href && window.location.pathname === href) {
        router.refresh();
        return;
      }
      router.push(href);
    });
  }

  return {
    pathname,
    selectedPath,
    pending,
    lockedRef,
    operationLock,
    navigate,
    captureHistory,
    recordNavigation,
    entryPath,
  };
}

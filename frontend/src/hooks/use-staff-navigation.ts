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
import { writeAddress } from "@/lib/browser-url";
import { staffRecordLocation } from "@/lib/staff/record-location";
import { staffWorkspacePath } from "@/lib/staff/navigation";
import {
  StaffEntryRequestsContext,
  StaffHistoryContext,
  StaffLocalHistoryContext,
} from "@/components/staff/StaffClientEntryBoundary";

export function useStaffNavigation() {
  const pathname = usePathname();
  const router = useRouter();
  const entries = useContext(StaffEntryRequestsContext);
  const historyRef = useContext(StaffHistoryContext);
  const localHistory = useContext(StaffLocalHistoryContext);
  const [guard] = useState(createInteractionGuard);
  const guardState = useSyncExternalStore(guard.subscribe, guard.getSnapshot, guard.getSnapshot);
  const confirmed = useRef<{ href: string; state: unknown } | null>(null);
  const previousPath = useRef(pathname);
  const [entryPath, setEntryPath] = useState<string | null>(null);
  const [pending, startTransition] = useTransition();
  const [selectedPath, selectPath] = useOptimistic(pathname);

  const captureHistory = useCallback(() => {
    confirmed.current = { href: window.location.href, state: window.history.state };
    previousPath.current = window.location.pathname;
  }, []);

  const setLocked = useCallback(
    (owner: symbol, locked: boolean | InteractionBlock | null) => {
      const before = guard.isLocked();
      guard.setLocked(owner, locked);
      if (guard.isLocked() && !before) {
        confirmed.current = { href: window.location.href, state: window.history.state };
      }
    },
    [guard],
  );
  const operationLock = useMemo(() => ({ setLocked, guard }), [setLocked, guard]);

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
    if (localHistory?.current?.(event)) {
      event.stopImmediatePropagation();
      previousPath.current = window.location.pathname;
      return;
    }
    if (!confirmed.current) {
      return;
    }
    const workspace = localHistory?.pathname ?? "/dashboard/intake";
    const previousRecord = staffRecordLocation(previousPath.current, workspace);
    const destinationUrl = new URL(window.location.href);
    const destinationRecord = staffRecordLocation(destinationUrl.pathname, workspace);
    if (previousRecord !== null && destinationRecord !== null) {
      const nextSelectedId = destinationRecord.id;
      const currentSelectedId = localHistory?.selectedId ?? previousRecord.id;
      if (
        !guard.isLocked() ||
        (!guard.getSnapshot().blocked && nextSelectedId === currentSelectedId)
      ) {
        confirmed.current = { href: window.location.href, state: window.history.state };
        previousPath.current = window.location.pathname;
        return;
      }
      event.stopImmediatePropagation();
      const href = `${destinationUrl.pathname}${destinationUrl.search}`;
      writeAddress(confirmed.current.href, confirmed.current.state, "push");
      guard.request(() => {
        writeAddress(href, null, "replace");
        confirmed.current = { href: window.location.href, state: window.history.state };
        previousPath.current = window.location.pathname;
      });
      return;
    }
    if (guard.isLocked()) {
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
    operationLock,
    guardState,
    navigate,
    captureHistory,
    recordNavigation,
    entryPath,
    localPath: localHistory?.pathname ?? staffWorkspacePath(pathname),
  };
}

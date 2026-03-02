"use client";

import { useCallback, useEffect, useOptimistic, useRef, useState, useTransition } from "react";
import { usePathname, useRouter } from "next/navigation";

export function useStaffNavigation() {
  const pathname = usePathname();
  const router = useRouter();
  const lockedRef = useRef(false);
  const confirmed = useRef<{ href: string; state: unknown } | null>(null);
  const previousPath = useRef(pathname);
  const [historyPath, setHistoryPath] = useState<string | null>(null);
  const [pending, startTransition] = useTransition();
  const [selectedPath, selectPath] = useOptimistic(pathname);

  const captureHistory = useCallback(() => {
    confirmed.current = { href: window.location.href, state: window.history.state };
  }, []);

  const recordNavigation = useCallback((href: string) => {
    previousPath.current = new URL(href, window.location.origin).pathname;
  }, []);

  useEffect(() => {
    function protectOperation(event: PopStateEvent) {
      if (!confirmed.current) {
        return;
      }
      if (lockedRef.current) {
        event.stopImmediatePropagation();
        window.history.pushState(confirmed.current.state, "", confirmed.current.href);
        return;
      }
      if (previousPath.current !== window.location.pathname) {
        setHistoryPath(window.location.pathname);
      }
      previousPath.current = window.location.pathname;
    }
    window.addEventListener("popstate", protectOperation, true);
    return () => window.removeEventListener("popstate", protectOperation, true);
  }, []);

  function navigate(event: { preventDefault: () => void }, href: string) {
    event.preventDefault();
    if (lockedRef.current) {
      return;
    }
    startTransition(() => {
      selectPath(href);
      if (pathname === href) {
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
    navigate,
    captureHistory,
    recordNavigation,
    historyPath,
    completeHistory: () => setHistoryPath(null),
  };
}

"use client";

import { useCallback, useEffect, useRef, useState, useTransition } from "react";
import { usePathname, useRouter } from "next/navigation";

export function useStaffNavigation() {
  const pathname = usePathname();
  const router = useRouter();
  const lockedRef = useRef(false);
  const confirmed = useRef<{ href: string; state: unknown } | null>(null);
  const [historyPath, setHistoryPath] = useState<string | null>(null);
  const [pending, startTransition] = useTransition();

  const captureHistory = useCallback(() => {
    confirmed.current = { href: window.location.href, state: window.history.state };
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
      if (new URL(confirmed.current.href).pathname !== window.location.pathname) {
        setHistoryPath(window.location.pathname);
      }
    }
    window.addEventListener("popstate", protectOperation, true);
    return () => window.removeEventListener("popstate", protectOperation, true);
  }, []);

  function navigate(event: { preventDefault: () => void }, href: string) {
    if (pathname === href) {
      event.preventDefault();
      startTransition(() => router.refresh());
    }
  }

  return {
    pathname,
    pending,
    lockedRef,
    navigate,
    captureHistory,
    historyPath,
    completeHistory: () => setHistoryPath(null),
  };
}

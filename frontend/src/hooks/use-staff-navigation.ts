"use client";

import { useEffect, useRef, useState } from "react";
import { usePathname, useRouter, useSearchParams } from "next/navigation";

export function useStaffNavigation() {
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const router = useRouter();
  const lockedRef = useRef(false);
  const confirmed = useRef<{ href: string; state: unknown } | null>(null);
  const [historyPath, setHistoryPath] = useState<string | null>(null);

  useEffect(() => {
    confirmed.current = { href: window.location.href, state: window.history.state };
  }, [pathname, searchParams]);

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
      router.refresh();
    }
  }

  return {
    pathname,
    lockedRef,
    navigate,
    historyPath,
    completeHistory: () => setHistoryPath(null),
  };
}

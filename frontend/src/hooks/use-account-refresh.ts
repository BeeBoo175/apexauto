"use client";

import { useEffect, useRef, useState } from "react";
import { useToast } from "@/components/ui/Toast";
import { AUTH_DEADLINE_MS, parseAccount } from "@/lib/auth/contracts";
import type { CustomerAccount } from "@/lib/auth/contracts";
import { sessionChanges } from "@/lib/auth/browser-session";

export function useAccountRefresh(initialAccount: CustomerAccount) {
  const [account, setAccount] = useState<CustomerAccount | null>(initialAccount);
  const [refreshing, setRefreshing] = useState(false);
  const [refreshFailed, setRefreshFailed] = useState(false);
  const active = useRef(true);
  const controller = useRef<AbortController | null>(null);
  const { error } = useToast();

  useEffect(() => {
    active.current = true;

    function invalidateActor() {
      active.current = false;
      controller.current?.abort();
      setAccount(null);
      setRefreshFailed(false);
      window.location.reload();
    }

    function restorePage(event: PageTransitionEvent) {
      if (event.persisted) {
        invalidateActor();
      }
    }

    sessionChanges?.addEventListener("message", invalidateActor);
    window.addEventListener("pageshow", restorePage);
    return () => {
      active.current = false;
      controller.current?.abort();
      sessionChanges?.removeEventListener("message", invalidateActor);
      window.removeEventListener("pageshow", restorePage);
    };
  }, []);

  async function refresh() {
    if (!active.current || controller.current) {
      return;
    }

    const attempt = new AbortController();
    controller.current = attempt;
    setRefreshing(true);
    const isCurrent = () =>
      active.current && controller.current === attempt && !attempt.signal.aborted;

    try {
      const response = await fetch("/api/account", {
        cache: "no-store",
        signal: AbortSignal.any([attempt.signal, AbortSignal.timeout(AUTH_DEADLINE_MS + 2000)]),
      });

      if (!isCurrent()) {
        return;
      }

      if (response.status === 401 || response.status === 403) {
        active.current = false;
        setAccount(null);
        setRefreshFailed(false);
        window.location.assign(new URL("/login?returnTo=%2Faccount", window.location.origin).href);
        return;
      }

      const updated = parseAccount(await response.json());

      if (!isCurrent()) {
        return;
      }

      if (!response.ok || !updated) {
        throw new Error("Account refresh failed");
      }

      if (updated.userId !== initialAccount.userId) {
        active.current = false;
        setAccount(null);
        setRefreshFailed(false);
        window.location.reload();
        return;
      }

      setAccount(updated);
      setRefreshFailed(false);
    } catch {
      if (isCurrent()) {
        setRefreshFailed(true);
        error("Your account could not be refreshed. The last confirmed details are still shown.");
      }
    } finally {
      if (isCurrent()) {
        setRefreshing(false);
      }

      if (controller.current === attempt) {
        controller.current = null;
      }
    }
  }

  return { account, refreshing, refreshFailed, refresh };
}

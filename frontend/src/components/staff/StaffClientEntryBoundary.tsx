"use client";

import { createContext, useEffect, useLayoutEffect, useRef, useState, type ReactNode } from "react";
import type { CurrentActor } from "@/lib/auth/actor";
import type { SearchParams } from "@/lib/query";

export const StaffHistoryContext = createContext<{
  current: ((event: PopStateEvent) => void) | null;
} | null>(null);

export function StaffHistoryBoundary({ children }: { children: ReactNode }) {
  const handler = useRef<((event: PopStateEvent) => void) | null>(null);
  useLayoutEffect(() => {
    // Keep registration ahead of the router even when the dashboard unmounts.
    const restore = (event: PopStateEvent) => handler.current?.(event);
    window.addEventListener("popstate", restore);
    return () => window.removeEventListener("popstate", restore);
  }, []);
  return <StaffHistoryContext.Provider value={handler}>{children}</StaffHistoryContext.Provider>;
}

export interface StaffEntryRequest<T> {
  controller: AbortController;
  promise: Promise<T | null>;
  failure?: unknown;
}

interface EntryRequests {
  isMounted: () => boolean;
  mount: () => () => void;
  open: <T>(id: string, read: (signal: AbortSignal) => Promise<T>) => StaffEntryRequest<T>;
  release: (id: string) => void;
  cancel: () => void;
}

export const StaffEntryRequestsContext = createContext<EntryRequests | null>(null);
export const StaffEntryIdentityContext = createContext<string | null>(null);
export const StaffEntryPresentationContext = createContext<{
  actor: CurrentActor;
  path: string;
  parameters: SearchParams;
} | null>(null);

function createRequests(): EntryRequests {
  let mounted = false;
  let pending: { id: string; request: StaffEntryRequest<unknown> } | null = null;

  function cancel() {
    pending?.request.controller.abort();
    pending = null;
  }

  return {
    isMounted: () => mounted,
    mount() {
      mounted = true;
      return () => {
        mounted = false;
        cancel();
      };
    },
    open<T>(id: string, read: (signal: AbortSignal) => Promise<T>) {
      if (pending?.id === id) {
        // A server entry identity owns exactly one feature read, including suspended retries.
        return pending.request as StaffEntryRequest<T>;
      }
      cancel();
      const controller = new AbortController();
      const signal = AbortSignal.any([controller.signal, AbortSignal.timeout(22_000)]);
      const promise = Promise.race([
        Promise.resolve().then(() => read(signal)),
        new Promise<never>((_, reject) => {
          signal.addEventListener("abort", () => reject(signal.reason), { once: true });
        }),
      ])
        .catch((error: unknown) => {
          request.failure = error;
          return null;
        })
        .finally(() => controller.abort());
      const request: StaffEntryRequest<T> = { controller, promise };
      pending = { id, request };
      return request;
    },
    release(id) {
      if (pending?.id === id) {
        pending = null;
      }
    },
    cancel,
  };
}

export function StaffClientEntryBoundary({ children }: { children: ReactNode }) {
  const [requests] = useState(createRequests);
  useEffect(() => requests.mount(), [requests]);
  return (
    <StaffEntryRequestsContext.Provider value={requests}>
      {children}
    </StaffEntryRequestsContext.Provider>
  );
}

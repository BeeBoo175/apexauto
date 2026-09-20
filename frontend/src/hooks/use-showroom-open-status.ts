"use client";

import { useMemo, useSyncExternalStore } from "react";
import { showroomOpenStatus, type ShowroomOpenStatus } from "@/lib/guide/showrooms";

const MINUTE_MS = 60_000;

function subscribe(onChange: () => void) {
  const timer = window.setInterval(onChange, MINUTE_MS);
  return () => window.clearInterval(timer);
}

// Whether the showrooms are open right now, refreshed each minute. It is null on the server and until the page is
// running, because the answer depends on the visitor's clock, so the first render and the server's agree.
export function useShowroomOpenStatus(): ShowroomOpenStatus | null {
  const minute = useSyncExternalStore(
    subscribe,
    () => Math.floor(Date.now() / MINUTE_MS),
    () => null,
  );
  return useMemo(
    () => (minute === null ? null : showroomOpenStatus(new Date(minute * MINUTE_MS))),
    [minute],
  );
}

"use client";

import { useSyncExternalStore } from "react";

// Reports whether a media query currently matches. The server and the first client render report false,
// so the wide layout is what is rendered before the browser has measured anything.
export function useMediaQuery(query: string) {
  return useSyncExternalStore(
    (notify) => {
      const list = window.matchMedia(query);
      list.addEventListener("change", notify);
      return () => list.removeEventListener("change", notify);
    },
    () => window.matchMedia(query).matches,
    () => false,
  );
}

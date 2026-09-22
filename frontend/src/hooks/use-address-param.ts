"use client";

import { useSyncExternalStore } from "react";

function subscribe(listener: () => void) {
  window.addEventListener("popstate", listener);
  return () => window.removeEventListener("popstate", listener);
}

// One address key read straight from the browser, empty on the server and until the page is running. Pages that
// are generated ahead of time use it to honour a link's key without reading search params on the server.
export function useAddressParam(key: string) {
  return useSyncExternalStore(
    subscribe,
    () => new URLSearchParams(window.location.search).get(key) ?? "",
    () => "",
  );
}

"use client";

import { useEffect } from "react";

export function useStaffViewTransitions() {
  useEffect(() => {
    function prepare(event: PageSwapEvent) {
      if (!event.viewTransition) {
        return;
      }
      const destination = event.activation?.entry?.url;
      if (!destination) {
        event.viewTransition.skipTransition();
        return;
      }
      const current = new URL(window.location.href);
      const next = new URL(destination);
      const switchesView =
        (current.searchParams.get("view") === "list") !==
        (next.searchParams.get("view") === "list");
      if (
        current.origin !== next.origin ||
        current.pathname !== "/dashboard/intake" ||
        next.pathname !== "/dashboard/intake" ||
        !switchesView
      ) {
        event.viewTransition.skipTransition();
      }
    }
    window.addEventListener("pageswap", prepare);
    return () => window.removeEventListener("pageswap", prepare);
  }, []);
}

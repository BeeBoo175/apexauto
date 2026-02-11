"use client";

import { useEffect, useState } from "react";
import {
  parseViewMemory,
  restoreViewHref,
  viewMemoryKey,
  viewQuery,
  type IntakeView,
} from "@/lib/staff/view-memory";

export function useStaffViewMemory(
  actor: string,
  active: IntakeView,
  destinations: Record<IntakeView, string>,
) {
  const inactive = active === "calendar" ? "list" : "calendar";
  const currentHref = destinations[active];
  const fallbackHref = destinations[inactive];
  const [restored, setRestored] = useState({ currentHref: "", actor: "", href: "" });

  useEffect(() => {
    let href = fallbackHref;
    try {
      const memory = parseViewMemory(window.sessionStorage.getItem(viewMemoryKey), actor);
      href = restoreViewHref(inactive, fallbackHref, memory[inactive]);
      window.sessionStorage.setItem(
        viewMemoryKey,
        JSON.stringify({
          actor,
          [active]: viewQuery(currentHref),
          [inactive]: viewQuery(href),
        }),
      );
    } catch {
      // Storage is optional; native links still work when the browser blocks it.
    }
    let disposed = false;
    queueMicrotask(() => {
      if (!disposed) {
        setRestored({ currentHref, actor, href });
      }
    });
    const current = new URL(window.location.href);
    const canonical = new URL(currentHref, current.origin);
    const appointment = current.searchParams.get("appointment");
    if (appointment) {
      canonical.searchParams.set("appointment", appointment);
    }
    canonical.hash = current.hash;
    if (current.search && canonical.href !== current.href) {
      window.history.replaceState(window.history.state, "", canonical);
    }
    return () => {
      disposed = true;
    };
  }, [actor, active, inactive, currentHref, fallbackHref]);

  const href =
    restored.currentHref === currentHref && restored.actor === actor ? restored.href : fallbackHref;
  return { ...destinations, [inactive]: href };
}

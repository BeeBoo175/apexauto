"use client";

import { useRouter } from "next/navigation";
import { useEffect, useRef, useState } from "react";

// An in-app link the guard held back until the customer confirms.
interface HeldLeave {
  href: string;
}

function isPlainLeftClick(event: MouseEvent) {
  return event.button === 0 && !event.metaKey && !event.ctrlKey && !event.shiftKey && !event.altKey;
}

// The customer's journey keeps everything in the browser until the booking, so while entered work exists every
// way out asks first: reloading, closing the tab or leaving the site (the browser's own prompt) and following a
// link on the site (the shared confirmation dialog, driven by the returned state). Back to an earlier page of
// the site is not intercepted: the router unmounts the journey before any listener of ours can ask.
export function useSellerLeaveGuard(active: boolean) {
  const router = useRouter();
  const [held, setHeld] = useState<HeldLeave | null>(null);
  const bypass = useRef(false);

  useEffect(() => {
    if (!active) {
      return;
    }
    const journeyPath = window.location.pathname;

    const beforeUnload = (event: BeforeUnloadEvent) => {
      event.preventDefault();
      event.returnValue = "";
    };

    const click = (event: MouseEvent) => {
      if (bypass.current || event.defaultPrevented || !isPlainLeftClick(event)) {
        return;
      }
      const link = event.target instanceof Element ? event.target.closest("a[href]") : null;
      if (!(link instanceof HTMLAnchorElement) || (link.target && link.target !== "_self")) {
        return;
      }
      const destination = new URL(link.href, window.location.href);
      // Moving between the journey's own stages changes only the query, and other origins use the browser prompt.
      if (destination.origin !== window.location.origin || destination.pathname === journeyPath) {
        return;
      }
      event.preventDefault();
      event.stopPropagation();
      setHeld({
        href: `${destination.pathname}${destination.search}${destination.hash}`,
      });
    };

    window.addEventListener("beforeunload", beforeUnload);
    document.addEventListener("click", click, true);
    return () => {
      window.removeEventListener("beforeunload", beforeUnload);
      document.removeEventListener("click", click, true);
    };
  }, [active]);

  function confirmLeave() {
    const leave = held;
    setHeld(null);
    if (!leave) {
      return;
    }
    bypass.current = true;
    router.push(leave.href);
  }

  return { leaving: held !== null, confirmLeave, stayHere: () => setHeld(null) };
}

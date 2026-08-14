"use client";

import { useRouter } from "next/navigation";
import { useEffect, useRef, useState } from "react";

// A leave the guard held back until the customer confirms: a link to follow, or a Back out of the page.
interface HeldLeave {
  href: string | null;
}

// The part of the Navigation API used here; TypeScript's DOM library does not describe it yet.
interface TraverseEvent extends Event {
  navigationType: string;
  destination: { url: string };
}

interface NavigationTarget {
  addEventListener: (type: "navigate", listener: (event: TraverseEvent) => void) => void;
  removeEventListener: (type: "navigate", listener: (event: TraverseEvent) => void) => void;
}

function isPlainLeftClick(event: MouseEvent) {
  return event.button === 0 && !event.metaKey && !event.ctrlKey && !event.shiftKey && !event.altKey;
}

// The customer's journey keeps everything in the browser until the booking, so while entered work exists every
// way out asks first: reloading, closing the tab or leaving the site (the browser's own prompt), and following a
// link or pressing Back out of the page (the shared confirmation dialog, driven by the returned state).
export function useSellerLeaveGuard(active: boolean) {
  const router = useRouter();
  const [held, setHeld] = useState<HeldLeave | null>(null);
  const bypass = useRef(false);

  useEffect(() => {
    if (!active) {
      return;
    }
    const journeyPath = window.location.pathname;
    // Browsers without the Navigation API keep the browser prompt and link guard only.
    const navigation = (window as Window & { navigation?: NavigationTarget }).navigation;

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

    // Back and Forward are held through the Navigation API: the router commits a traversal before popstate is
    // dispatched, so a popstate listener would hear it only after the journey is gone.
    const traverse = (event: TraverseEvent) => {
      if (bypass.current || event.navigationType !== "traverse" || !event.cancelable) {
        return;
      }
      if (new URL(event.destination.url).pathname === journeyPath) {
        return;
      }
      event.preventDefault();
      setHeld({ href: null });
    };

    window.addEventListener("beforeunload", beforeUnload);
    document.addEventListener("click", click, true);
    navigation?.addEventListener("navigate", traverse);
    return () => {
      window.removeEventListener("beforeunload", beforeUnload);
      document.removeEventListener("click", click, true);
      navigation?.removeEventListener("navigate", traverse);
    };
  }, [active]);

  function confirmLeave() {
    const leave = held;
    setHeld(null);
    if (!leave) {
      return;
    }
    bypass.current = true;
    if (leave.href === null) {
      window.history.back();
      return;
    }
    router.push(leave.href);
  }

  return { leaving: held !== null, confirmLeave, stayHere: () => setHeld(null) };
}

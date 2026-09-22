"use client";

import { prefersReducedMotion, TABBED_LAYOUT_QUERY } from "@/lib/motion";
import { useLayoutEffect, useRef, useState } from "react";
import {
  isVehicleDetailSection,
  vehicleDetailSections,
  type VehicleDetailSection,
} from "@/lib/vehicles/contracts";
import { writeAddress } from "@/lib/browser-url";

// Below this width the sections are tabs: only the chosen one is shown.
function isTabbedLayout() {
  return window.matchMedia(TABBED_LAYOUT_QUERY).matches;
}

function updateSectionUrl(section: VehicleDetailSection | null) {
  const url = new URL(window.location.href);
  const hash = section ? `#${section}` : "";
  if (url.hash === hash && !url.searchParams.has("section")) {
    return;
  }

  url.searchParams.delete("section");
  url.hash = hash;
  writeAddress(url, window.history.state);
}

export function useVehicleDetailSections() {
  const [section, setSection] = useState<VehicleDetailSection>("overview");
  const destination = useRef<VehicleDetailSection | null>(null);

  function select(next: VehicleDetailSection) {
    const panel = document.getElementById(next);
    if (!panel) {
      return;
    }

    destination.current = next;
    setSection(next);
    updateSectionUrl(next);
    // Phones show one section at a time, so the tab bar, not the hidden panel, is the scroll target.
    const target = isTabbedLayout() ? document.getElementById(`vehicle-tab-${next}`) : panel;
    if (!isTabbedLayout()) {
      panel.focus({ preventScroll: true });
    }
    target?.scrollIntoView({
      block: "start",
      behavior: prefersReducedMotion() ? "instant" : "smooth",
    });
  }

  useLayoutEffect(() => {
    function restoreSelection() {
      const value = window.location.hash.slice(1);
      destination.current = null;
      if (isVehicleDetailSection(value)) {
        setSection(value);
        return;
      }
      setSection("overview");
    }

    function releaseDestination() {
      destination.current = null;
    }

    function interrupt(event: KeyboardEvent) {
      if (["ArrowUp", "ArrowDown", "PageUp", "PageDown", "Home", "End", " "].includes(event.key)) {
        releaseDestination();
      }
    }

    function track() {
      const overview = document.getElementById("overview");
      if (destination.current || !overview || isTabbedLayout()) {
        return;
      }

      const headerHeight = Number.parseFloat(
        getComputedStyle(document.documentElement).getPropertyValue("--site-header-height"),
      );
      const threshold = headerHeight + 80;
      if (window.scrollY <= 1 || overview.getBoundingClientRect().top > threshold) {
        setSection("overview");
        updateSectionUrl(null);
        return;
      }

      const active = vehicleDetailSections.filter((id) => {
        const panel = document.getElementById(id);
        return panel && panel.getBoundingClientRect().top <= threshold;
      });
      const atBottom =
        window.scrollY + window.innerHeight >= document.documentElement.scrollHeight - 2;
      const next = atBottom ? "showroom" : (active.at(-1) ?? "overview");
      setSection(next);
      updateSectionUrl(next);
    }

    function finishScroll() {
      releaseDestination();
      track();
    }

    // Initial fragments target server-rendered anchors; hydration only synchronizes selection.
    restoreSelection();
    window.addEventListener("scroll", track, { passive: true });
    window.addEventListener("scrollend", finishScroll);
    window.addEventListener("wheel", releaseDestination, { passive: true });
    window.addEventListener("touchstart", releaseDestination, { passive: true });
    window.addEventListener("pointerdown", releaseDestination);
    window.addEventListener("keydown", interrupt);
    window.addEventListener("hashchange", restoreSelection);
    window.addEventListener("popstate", restoreSelection);
    return () => {
      window.removeEventListener("scroll", track);
      window.removeEventListener("scrollend", finishScroll);
      window.removeEventListener("wheel", releaseDestination);
      window.removeEventListener("touchstart", releaseDestination);
      window.removeEventListener("pointerdown", releaseDestination);
      window.removeEventListener("keydown", interrupt);
      window.removeEventListener("hashchange", restoreSelection);
      window.removeEventListener("popstate", restoreSelection);
    };
  }, []);

  return { section, select };
}

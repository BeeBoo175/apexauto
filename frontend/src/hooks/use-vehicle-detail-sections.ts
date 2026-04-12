"use client";

import { useLayoutEffect, useRef, useState } from "react";
import {
  isVehicleDetailSection,
  vehicleDetailSections,
  type VehicleDetailSection,
} from "@/lib/vehicles/contracts";

function updateSectionUrl(section: VehicleDetailSection | null) {
  const url = new URL(window.location.href);
  const hash = section ? `#${section}` : "";
  if (url.hash === hash && !url.searchParams.has("section")) {
    return;
  }

  url.searchParams.delete("section");
  url.hash = hash;
  window.history.replaceState(window.history.state, "", url);
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
    panel.focus({ preventScroll: true });
    panel.scrollIntoView({
      block: "start",
      behavior: window.matchMedia("(prefers-reduced-motion: reduce)").matches
        ? "instant"
        : "smooth",
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
      if (destination.current || !overview) {
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
      const next = atBottom ? "trade-in" : (active.at(-1) ?? "overview");
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

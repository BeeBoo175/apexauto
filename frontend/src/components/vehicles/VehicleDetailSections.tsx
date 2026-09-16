"use client";

import { useEffect, useRef, useState, type MouseEvent, type ReactNode } from "react";
import { vehicleDetailSections, type VehicleDetailSection } from "@/lib/vehicles/contracts";
import styles from "./VehicleDetailSections.module.css";

const sectionLabels: Record<VehicleDetailSection, string> = {
  overview: "Overview",
  inspection: "Inspection report",
  financing: "Financing",
  showroom: "Showroom",
};

export function VehicleDetailSections({
  section,
  select,
  overview,
  inspection,
  financing,
  showroom,
}: {
  section: VehicleDetailSection;
  select: (section: VehicleDetailSection) => void;
  overview: ReactNode;
  inspection: ReactNode;
  financing: ReactNode;
  showroom: ReactNode;
}) {
  const panels = { overview, inspection, financing, showroom };

  function navigate(event: MouseEvent<HTMLAnchorElement>, next: VehicleDetailSection) {
    if (event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) {
      return;
    }
    event.preventDefault();
    select(next);
  }

  const sentinel = useRef<HTMLDivElement>(null);
  const [stuck, setStuck] = useState(false);

  // The tab bar is stuck once the marker above it scrolls beneath the site header.
  useEffect(() => {
    const marker = sentinel.current;
    if (!marker) {
      return;
    }
    const headerHeight = Number.parseFloat(
      getComputedStyle(document.documentElement).getPropertyValue("--site-header-height"),
    );
    const offset = Number.isFinite(headerHeight) ? headerHeight : 0;

    function measure() {
      setStuck(marker!.getBoundingClientRect().top <= offset);
    }

    measure();
    window.addEventListener("scroll", measure, { passive: true });
    window.addEventListener("resize", measure);
    return () => {
      window.removeEventListener("scroll", measure);
      window.removeEventListener("resize", measure);
    };
  }, []);

  return (
    <div className={styles.sections}>
      <div ref={sentinel} className={styles.sentinel} aria-hidden="true" />
      <nav className={styles.tabs} data-stuck={stuck || undefined} aria-label="Vehicle information">
        {vehicleDetailSections.map((id) => (
          <a
            key={id}
            id={`vehicle-tab-${id}`}
            href={`#${id}`}
            aria-current={section === id ? "location" : undefined}
            onClick={(event) => navigate(event, id)}
          >
            {sectionLabels[id]}
          </a>
        ))}
      </nav>
      {vehicleDetailSections.map((id) => (
        <div key={id} id={id} tabIndex={-1} className={styles.panel} data-section={id}>
          {panels[id]}
        </div>
      ))}
    </div>
  );
}

import type { MouseEvent, ReactNode } from "react";
import { vehicleDetailSections, type VehicleDetailSection } from "@/lib/vehicles/contracts";
import styles from "./VehicleDetailSections.module.css";

const sectionLabels: Record<VehicleDetailSection, string> = {
  overview: "Overview",
  inspection: "Inspection report",
  financing: "Financing",
  "trade-in": "Trade-in",
};

export function VehicleDetailSections({
  section,
  select,
  overview,
  inspection,
  financing,
  tradeIn,
}: {
  section: VehicleDetailSection;
  select: (section: VehicleDetailSection) => void;
  overview: ReactNode;
  inspection: ReactNode;
  financing: ReactNode;
  tradeIn: ReactNode;
}) {
  const panels = { overview, inspection, financing, "trade-in": tradeIn };

  function navigate(event: MouseEvent<HTMLAnchorElement>, next: VehicleDetailSection) {
    if (event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) {
      return;
    }
    event.preventDefault();
    select(next);
  }

  return (
    <div className={styles.sections}>
      <nav className={styles.tabs} aria-label="Vehicle information">
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

import type { MouseEvent, ReactNode } from "react";
import { vehicleDetailSections, type VehicleDetailSection } from "@/lib/vehicles/contracts";
import styles from "./VehicleDetailSections.module.css";
import layout from "./VehicleLayout.module.css";

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
        <div className={`${layout.content} ${styles.tabsInner}`}>
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
        </div>
      </nav>
      {vehicleDetailSections.map((id) => (
        <div key={id} id={id} tabIndex={-1} className={styles.panel} data-section={id}>
          <div className={layout.content}>
            {id === "inspection" ? (
              <div className={styles.inspectionCard}>{panels[id]}</div>
            ) : (
              panels[id]
            )}
          </div>
        </div>
      ))}
    </div>
  );
}

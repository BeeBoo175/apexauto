"use client";

import { useState } from "react";
import Link from "next/link";
import { Button, buttonClassName } from "@/components/ui/Button";
import { Icon } from "@/components/ui/Icon";
import { inspectionCategoryIcons, label } from "@/lib/inspection/contracts";
import styles from "./LandingInspectionOverview.module.css";
import layout from "./VehicleLayout.module.css";

interface InspectionArea {
  id: keyof typeof inspectionCategoryIcons;
  count: number;
  summary: string;
  samples: readonly { code: string; description: string }[];
}

const INSPECTION_AREAS: readonly InspectionArea[] = [
  {
    id: "FrameChassis",
    count: 45,
    summary:
      "Technicians verify VIN identity, structural rails, pillars, cut-and-weld seams and flood waterline indicators.",
    samples: [
      { code: "FC-001", description: "VIN stamping identity and legibility" },
      { code: "FC-003", description: "Front left chassis rail shape and alignment" },
      { code: "FC-038", description: "Evidence of structural cut-and-weld joining" },
      { code: "FC-042", description: "Cabin sediment or waterline evidence" },
    ],
  },
  {
    id: "EngineTransmission",
    count: 50,
    summary:
      "Cold start, idle stability, fluid contamination, diagnostic readiness and controlled road gear-change checks.",
    samples: [
      { code: "ET-002", description: "Cold-start behavior" },
      { code: "ET-009", description: "Engine oil leakage" },
      { code: "ET-027", description: "Engine diagnostic fault evidence" },
      { code: "ET-043", description: "Gear change quality" },
    ],
  },
  {
    id: "ExteriorPaint",
    count: 45,
    summary:
      "Panel gap alignment, paint thickness consistency across panels, glazing, lamps and exterior seal integrity.",
    samples: [
      { code: "EP-002", description: "Bonnet paint finish consistency" },
      { code: "EP-039", description: "Paint thickness consistency across comparable panels" },
      { code: "EP-040", description: "Panel gap and flushness consistency" },
      { code: "EP-045", description: "External water ingress evidence at openings" },
    ],
  },
  {
    id: "InteriorElectrical",
    count: 40,
    summary:
      "Odometer consistency, airbag diagnostics, safety belts, air-conditioning cooling and cabin electronics.",
    samples: [
      { code: "IE-002", description: "Mileage consistency with available retained records" },
      { code: "IE-006", description: "Airbag warning and diagnostic evidence" },
      { code: "IE-017", description: "Air-conditioning cooling performance" },
      { code: "IE-037", description: "Parking sensors and reversing camera where fitted" },
    ],
  },
  {
    id: "UnderbodyBrakesSuspension",
    count: 30,
    summary:
      "Tyre tread pairing, brake friction and discs, ABS diagnostics, suspension dampers and steering rack checks.",
    samples: [
      { code: "UB-005", description: "Tyre size, load suitability and axle pairing" },
      { code: "UB-009", description: "Front brake friction material condition" },
      { code: "UB-018", description: "ABS warning and diagnostic evidence" },
      {
        code: "UB-030",
        description:
          "Directional stability, braking pull or suspension noise during controlled assessment",
      },
    ],
  },
];

export function LandingInspectionOverview() {
  const [activeAreaId, setActiveAreaId] = useState(INSPECTION_AREAS[0].id);
  const activeArea =
    INSPECTION_AREAS.find((area) => area.id === activeAreaId) ?? INSPECTION_AREAS[0];
  const activeLabel = label(activeArea.id);

  return (
    <section
      className={`${layout.band} ${styles.section}`}
      aria-labelledby="inspection-overview-heading"
    >
      <div className={`${layout.content} ${styles.inner}`}>
        <div className={styles.copy}>
          <h2
            id="inspection-overview-heading"
            className={`${layout.displayHeading} ${styles.overviewTitle}`}
          >
            Signed 210-point inspection on every listing
          </h2>
          <p className={styles.intro}>
            Every listed vehicle carries a technician-signed inspection ledger published directly on
            its page, so you can inspect recorded findings and pricing before paying a deposit.
          </p>
          <ul className={styles.guarantees}>
            <li>
              <span className={styles.guaranteeIcon} aria-hidden="true">
                <Icon name="inspection" />
              </span>
              <div>
                <strong>Full checkpoint visibility</strong>
                <p>
                  Search all 210 items or filter specifically for recorded findings and repair
                  estimates on each vehicle page.
                </p>
              </div>
            </li>
            <li>
              <span className={styles.guaranteeIcon} aria-hidden="true">
                <Icon name="car" />
              </span>
              <div>
                <strong>Upfront itemized MYR pricing</strong>
                <p>
                  Vehicle price, processing fee, road tax and applicable SST appear clearly before
                  you commit to a hold.
                </p>
              </div>
            </li>
            <li>
              <span className={styles.guaranteeIcon} aria-hidden="true">
                <Icon name="calendar" />
              </span>
              <div>
                <strong>48-hour online hold or test drive</strong>
                <p>
                  Lock an available car with a MYR 500 simulated deposit or schedule a showroom test
                  drive directly from the listing.
                </p>
              </div>
            </li>
          </ul>
          <div className={styles.actions}>
            <Link href="/vehicles" prefetch={false} className={buttonClassName("primary", "md")}>
              Browse inspected vehicles
            </Link>
            <Link href="/buying-guide" className={buttonClassName("shell", "md")}>
              Read buying guide
            </Link>
          </div>
        </div>

        <div className={styles.explorer} aria-label="210-point inspection area explorer">
          <div className={styles.categories} role="group" aria-label="Inspection categories">
            {INSPECTION_AREAS.map((area) => {
              const selected = area.id === activeArea.id;
              return (
                <Button
                  key={area.id}
                  type="button"
                  variant="choice"
                  size="tile"
                  aria-pressed={selected}
                  onClick={() => setActiveAreaId(area.id)}
                >
                  <Icon name={inspectionCategoryIcons[area.id]} />
                  <span>
                    {label(area.id)}
                    <small>{area.count} checkpoints</small>
                  </span>
                </Button>
              );
            })}
          </div>

          <div className={styles.detail} role="region" aria-live="polite" aria-label={activeLabel}>
            <header className={styles.detailHeader}>
              <h3>{activeLabel}</h3>
              <span className={styles.pointCount}>{activeArea.count} checks in report</span>
            </header>
            <p className={styles.detailSummary}>{activeArea.summary}</p>
            <ul className={styles.checkpoints}>
              {activeArea.samples.map((sample) => (
                <li key={sample.code}>
                  <span className={styles.code}>{sample.code}</span>
                  <span>{sample.description}</span>
                </li>
              ))}
            </ul>
            <p className={styles.detailFoot}>
              Open any car listing to inspect all 210 signed checkpoints and technician notes.
            </p>
          </div>
        </div>
      </div>
    </section>
  );
}

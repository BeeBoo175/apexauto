"use client";

import Image from "next/image";
import { useState, type KeyboardEvent } from "react";
import patterns from "@/components/layout/PublicPatterns.module.css";
import { Icon } from "@/components/ui/Icon";
import { ScrollStrip } from "@/components/ui/ScrollStrip";
import { SearchField } from "@/components/ui/SearchField";
import layout from "@/components/vehicles/VehicleLayout.module.css";
import type { InspectionCategoryKey, InspectionPointItem } from "@/lib/guide/inspection-points";
import { inspectionCategories } from "@/lib/guide/inspection-rules";
import styles from "./InspectionAreas.module.css";

const COLLAPSED_ROWS = 12;

// Where each area sits on the cutaway picture, as percentages of the picture itself.
const PIN_POSITIONS: Record<InspectionCategoryKey, { left: number; top: number }> = {
  FrameChassis: { left: 39.5, top: 68.5 },
  EngineTransmission: { left: 35.1, top: 57.9 },
  ExteriorPaint: { left: 69.3, top: 57.9 },
  InteriorElectrical: { left: 52.6, top: 42.9 },
  UnderbodyBrakesSuspension: { left: 54.4, top: 68.5 },
};

const SHORT_NAMES: Record<InspectionCategoryKey, string> = {
  FrameChassis: "Frame",
  EngineTransmission: "Engine",
  ExteriorPaint: "Exterior",
  InteriorElectrical: "Interior",
  UnderbodyBrakesSuspension: "Underbody",
};

function Tick() {
  return (
    <svg viewBox="0 0 16 16" className={styles.tick} aria-hidden="true" focusable="false">
      <path d="M4 8.5l2.5 2.5L12 5.5" />
    </svg>
  );
}

// Where on the car, and what is checked there. A picture of the car carries a pin per area; the pins and the icon
// tabs choose the same area, and the checkpoints of that area are listed beside it.
export function InspectionAreas({ points }: { points: readonly InspectionPointItem[] }) {
  const categories = inspectionCategories(points);
  const [activeKey, setActiveKey] = useState<InspectionCategoryKey>("FrameChassis");
  const [query, setQuery] = useState("");
  const [expanded, setExpanded] = useState(false);

  const active = categories.find((category) => category.key === activeKey) ?? categories[0];
  const term = query.trim().toLowerCase();
  const areaPoints = points.filter((point) => point.category === active.key);
  const matching = term
    ? areaPoints.filter(
        (point) =>
          point.code.toLowerCase().includes(term) || point.description.toLowerCase().includes(term),
      )
    : areaPoints;
  const shown = expanded || term ? matching : matching.slice(0, COLLAPSED_ROWS);

  function choose(key: InspectionCategoryKey) {
    setActiveKey(key);
    setQuery("");
    setExpanded(false);
  }

  function onKeyDown(event: KeyboardEvent<HTMLDivElement>) {
    const offsets: Record<string, number> = { ArrowRight: 1, ArrowLeft: -1 };
    const offset = offsets[event.key];
    if (offset === undefined) {
      return;
    }
    event.preventDefault();
    const index = categories.findIndex((category) => category.key === active.key);
    const next = categories[(index + offset + categories.length) % categories.length];
    choose(next.key);
    document.getElementById(`area-tab-${next.key}`)?.focus();
  }

  return (
    <section
      id="areas"
      className={`${patterns.band} ${patterns.bandPaper}`}
      aria-labelledby="inspection-areas-heading"
    >
      <div className={`${layout.content} ${styles.areas}`}>
        <h2 id="inspection-areas-heading" className={layout.sectionHeading}>
          Where we check
        </h2>

        <div className={styles.console}>
          <div className={styles.stageColumn}>
            <div className={styles.stage}>
              <div className={styles.figure}>
                <Image
                  src="/images/inspection-cutaway-centered.jpg"
                  alt="A cutaway view of a car with the five inspection areas marked"
                  fill
                  quality={85}
                  sizes="(max-width: 960px) 100vw, 520px"
                  className={styles.photo}
                />
                {categories.map((category, index) => (
                  <button
                    key={category.key}
                    type="button"
                    className={styles.pin}
                    style={{
                      left: `${PIN_POSITIONS[category.key].left}%`,
                      top: `${PIN_POSITIONS[category.key].top}%`,
                    }}
                    aria-label={category.title}
                    aria-pressed={category.key === active.key}
                    onClick={() => choose(category.key)}
                  >
                    {index + 1}
                  </button>
                ))}
              </div>
            </div>
          </div>

          <div className={styles.detail}>
            <div className={styles.tabsFrame} onKeyDown={onKeyDown}>
              <ScrollStrip
                label="Inspection areas"
                role="tablist"
                className={styles.tabs}
                dragScroll
              >
                {categories.map((category) => (
                  <button
                    key={category.key}
                    type="button"
                    role="tab"
                    id={`area-tab-${category.key}`}
                    aria-selected={category.key === active.key}
                    aria-controls="area-panel"
                    tabIndex={category.key === active.key ? 0 : -1}
                    className={styles.tab}
                    onClick={() => choose(category.key)}
                  >
                    <span className={styles.icon}>
                      <Icon name={category.icon} />
                    </span>
                    <span className={styles.tabName}>{SHORT_NAMES[category.key]}</span>
                  </button>
                ))}
              </ScrollStrip>
            </div>

            <div
              id="area-panel"
              role="tabpanel"
              aria-labelledby={`area-tab-${active.key}`}
              className={styles.panel}
            >
              <header key={active.key} className={styles.panelHead}>
                <h3>{active.title}</h3>
                <span className={styles.total}>{active.count} checkpoints</span>
              </header>
              <p key={`${active.key}-focus`} className={styles.focus}>
                {active.focus}
              </p>

              <SearchField
                label="Search this area"
                value={query}
                disabled={false}
                onChange={setQuery}
                onClear={query ? () => setQuery("") : undefined}
                placeholder="Try brake, paint or a code"
              />

              <ul key={`${active.key}-list`} className={styles.points}>
                {shown.map((point) => (
                  <li key={point.code}>
                    <Tick />
                    <span className={styles.description}>{point.description}</span>
                    {point.disqualifierRisk ? (
                      <span className={styles.fail}>Instant fail</span>
                    ) : (
                      <span className={styles.code}>{point.code}</span>
                    )}
                  </li>
                ))}
                {shown.length === 0 && (
                  <li className={styles.empty}>No checkpoint matches “{query}”.</li>
                )}
              </ul>

              <footer className={styles.panelFoot}>
                <button
                  type="button"
                  className={styles.more}
                  disabled={Boolean(term) || matching.length <= COLLAPSED_ROWS}
                  aria-expanded={expanded}
                  onClick={() => setExpanded(!expanded)}
                >
                  {expanded ? "Show fewer" : `Show all ${matching.length}`}
                </button>
                <span className={styles.legend}>
                  <span className={styles.fail}>Instant fail</span> means a critical finding here
                  rejects the car.
                </span>
              </footer>
            </div>
          </div>
        </div>
      </div>
    </section>
  );
}

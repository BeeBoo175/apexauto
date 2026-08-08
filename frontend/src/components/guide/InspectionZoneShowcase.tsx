"use client";

import { useRef, useState, type KeyboardEvent } from "react";
import Image, { type StaticImageData } from "next/image";
import brakesPhoto from "../../../public/images/inspect-brakes.jpg";
import chassisPhoto from "../../../public/images/apexauto-detail.webp";
import enginePhoto from "../../../public/images/inspect-engine.jpg";
import exteriorPhoto from "../../../public/images/inspect-exterior.jpg";
import interiorPhoto from "../../../public/images/inspect-interior.jpg";
import { Icon } from "@/components/ui/Icon";
import layout from "@/components/vehicles/VehicleLayout.module.css";
import type { InspectionCategoryKey } from "@/lib/guide/inspection-points";
import { inspectionCategories } from "@/lib/guide/inspection-rules";
import styles from "./GuideInspection.module.css";

interface ZoneDetails {
  photo: StaticImageData;
  photoAlt: string;
  samples: readonly string[];
}

const zoneMedia: Record<InspectionCategoryKey, ZoneDetails> = {
  FrameChassis: {
    photo: chassisPhoto,
    photoAlt: "Close-up of vehicle body structure, grille, and panel seams",
    samples: [
      "VIN stampings and 4-corner chassis alignment",
      "A/B/C pillars, roof rails, and structural sills",
      "Factory weld seams and hidden flood cavities",
    ],
  },
  EngineTransmission: {
    photo: enginePhoto,
    photoAlt: "Clean automotive engine compartment inspected for fluid seals and wiring",
    samples: [
      "Cold start, idle steadiness, and oil seals",
      "Radiator hoses, coolant purity, and cooling fans",
      "Gearbox engagement, clutch wear, and driveline load",
    ],
  },
  ExteriorPaint: {
    photo: exteriorPhoto,
    photoAlt: "Close detail of automotive paintwork finish, panel gap flushness, and headlamp",
    samples: [
      "Panel gap alignment, latches, and door hinges",
      "Glazing, wing mirrors, and all exterior lamps",
      "Paint-depth readings, filler, and overspray checks",
    ],
  },
  InteriorElectrical: {
    photo: interiorPhoto,
    photoAlt: "Digital instrument cluster, steering wheel, and interior cabin trim",
    samples: [
      "Verified odometer history and airbag diagnostics",
      "Safety-belt retractors, mounts, and dry carpets",
      "Cabin air-con cooling, windows, cameras, and battery",
    ],
  },
  UnderbodyBrakesSuspension: {
    photo: brakesPhoto,
    photoAlt: "Close-up of alloy wheel, perforated disc rotor, brake caliper, and tyre",
    samples: [
      "Tyre tread depth, axle matching, and wheel fasteners",
      "Brake pads, rotors, hydraulic lines, and ABS",
      "Coil springs, dampers, steering play, and road tracking",
    ],
  },
};

export function InspectionZoneShowcase() {
  const [activeKey, setActiveKey] = useState<InspectionCategoryKey>("FrameChassis");
  const tabRefs = useRef<Partial<Record<InspectionCategoryKey, HTMLButtonElement | null>>>({});

  const activeCategory =
    inspectionCategories.find((item) => item.key === activeKey) ?? inspectionCategories[0];
  const details = zoneMedia[activeKey];

  function activateByIndex(nextIndex: number) {
    const count = inspectionCategories.length;
    const normalizedIndex = ((nextIndex % count) + count) % count;
    const nextCategory = inspectionCategories[normalizedIndex];
    setActiveKey(nextCategory.key);
    tabRefs.current[nextCategory.key]?.focus();
  }

  function handleTabKeyDown(event: KeyboardEvent<HTMLButtonElement>, currentIndex: number) {
    if (event.key === "ArrowDown" || event.key === "ArrowRight") {
      event.preventDefault();
      activateByIndex(currentIndex + 1);
      return;
    }
    if (event.key === "ArrowUp" || event.key === "ArrowLeft") {
      event.preventDefault();
      activateByIndex(currentIndex - 1);
      return;
    }
    if (event.key === "Home") {
      event.preventDefault();
      activateByIndex(0);
      return;
    }
    if (event.key === "End") {
      event.preventDefault();
      activateByIndex(inspectionCategories.length - 1);
    }
  }

  return (
    <div className={styles.zoneShowcase}>
      <div className={styles.zoneTabs} role="tablist" aria-label="Vehicle inspection zones">
        {inspectionCategories.map((category, index) => {
          const selected = category.key === activeKey;
          return (
            <button
              key={category.key}
              id={`zone-tab-${category.key}`}
              ref={(element) => {
                tabRefs.current[category.key] = element;
              }}
              type="button"
              role="tab"
              aria-selected={selected}
              aria-controls="inspection-zone-panel"
              tabIndex={selected ? 0 : -1}
              className={styles.zoneTabButton}
              onClick={() => setActiveKey(category.key)}
              onKeyDown={(event) => handleTabKeyDown(event, index)}
            >
              <span className={styles.zoneTabLeft}>
                <Icon name={category.icon} />
                <span className={styles.zoneTabTitle}>{category.title}</span>
              </span>
              <span className={styles.zoneTabCount}>{category.count}</span>
            </button>
          );
        })}
      </div>

      <div
        id="inspection-zone-panel"
        role="tabpanel"
        aria-labelledby={`zone-tab-${activeCategory.key}`}
        tabIndex={0}
        className={styles.zoneStage}
      >
        <div className={styles.zonePhotoWrap}>
          <Image
            src={details.photo}
            alt={details.photoAlt}
            sizes="(max-width: 960px) 100vw, 520px"
          />
        </div>

        <div className={styles.zoneBody}>
          <h3 className={layout.cardHeading}>
            {activeCategory.title} ({activeCategory.count} points)
          </h3>
          <p className={styles.zoneFocus}>{activeCategory.focus}</p>
          <ul className={styles.zoneSamples}>
            {details.samples.map((sample) => (
              <li key={sample}>
                <Icon name="check" />
                <span>{sample}</span>
              </li>
            ))}
          </ul>
          <a href="#catalog" className={layout.arrowLink}>
            <span>View all {activeCategory.count} items</span>
            <Icon name="next" />
          </a>
        </div>
      </div>
    </div>
  );
}

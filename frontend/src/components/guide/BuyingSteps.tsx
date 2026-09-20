"use client";

import Image from "next/image";
import { useId, useState, type KeyboardEvent, type ReactNode } from "react";
import { Icon } from "@/components/ui/Icon";
import type { IconName } from "@/lib/icons";
import layout from "@/components/vehicles/VehicleLayout.module.css";
import styles from "./BuyingSteps.module.css";

export interface BuyingStepPanel {
  id: string;
  label: string;
  // Marks the tab with an icon; without one the tab shows its number.
  icon?: IconName;
  photo: { src: string; alt: string; position: string };
  content: ReactNode;
}

// One step is shown at a time. The numbered tabs switch between them and the arrow beside the photograph
// moves on to the next step, so the reader is never faced with all four at once.
export function BuyingSteps({ steps }: { steps: readonly BuyingStepPanel[] }) {
  const [active, setActive] = useState(0);
  const baseId = useId();
  const last = steps.length - 1;

  function select(index: number) {
    setActive(Math.min(Math.max(index, 0), last));
  }

  function onKeyDown(event: KeyboardEvent<HTMLDivElement>) {
    const offsets: Record<string, number> = { ArrowRight: 1, ArrowLeft: -1 };
    const offset = offsets[event.key];
    if (offset === undefined) {
      return;
    }
    event.preventDefault();
    const target = Math.min(Math.max(active + offset, 0), last);
    select(target);
    document.getElementById(`${baseId}-tab-${target}`)?.focus();
  }

  return (
    <div className={styles.steps}>
      <div className={styles.tabs} role="tablist" aria-label="Buying steps" onKeyDown={onKeyDown}>
        {steps.map((step, index) => (
          <button
            key={step.id}
            id={`${baseId}-tab-${index}`}
            type="button"
            role="tab"
            aria-selected={index === active}
            aria-controls={`${baseId}-panel-${index}`}
            tabIndex={index === active ? 0 : -1}
            className={styles.tab}
            onClick={() => select(index)}
          >
            <span className={styles.marker} aria-hidden="true">
              {step.icon ? <Icon name={step.icon} /> : index + 1}
            </span>
            {step.label}
          </button>
        ))}
      </div>

      <div className={styles.panels}>
        {steps.map((step, index) => (
          <section
            key={step.id}
            id={`${baseId}-panel-${index}`}
            role="tabpanel"
            aria-labelledby={`${baseId}-tab-${index}`}
            inert={index !== active}
            data-active={index === active || undefined}
            className={styles.panel}
          >
            <div className={styles.copy}>
              <span className={styles.badge}>Step {index + 1}</span>
              <h2 className={`${layout.sectionHeading} ${styles.title}`}>{step.label}</h2>
              {step.content}
            </div>
            <div className={styles.media}>
              <Image
                src={step.photo.src}
                alt={step.photo.alt}
                fill
                quality={85}
                sizes="(max-width: 900px) 100vw, 560px"
                style={{ objectPosition: step.photo.position }}
                className={styles.image}
              />
              {index < last && (
                <button
                  type="button"
                  className={styles.next}
                  aria-label={`Next step: ${steps[index + 1].label}`}
                  onClick={() => select(index + 1)}
                >
                  <Icon name="next" />
                </button>
              )}
            </div>
          </section>
        ))}
      </div>
    </div>
  );
}

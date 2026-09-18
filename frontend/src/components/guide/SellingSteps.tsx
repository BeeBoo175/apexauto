"use client";

import Image from "next/image";
import Link from "next/link";
import { useState } from "react";
import { buttonClassName } from "@/components/ui/Button";
import styles from "./SellingSteps.module.css";

export interface SellingStep {
  id: string;
  label: string;
  photo: { src: string; alt: string; position: string };
  text: string;
  fieldsTitle: string;
  fields: readonly string[];
  note: string;
}

// The four steps as photograph panels side by side: the chosen one opens to its details while the others fold
// down to a number and a name. On a phone they stack and open downward.
export function SellingSteps({ steps }: { steps: readonly SellingStep[] }) {
  const [active, setActive] = useState(0);

  return (
    <ol className={styles.track}>
      {steps.map((step, index) => {
        const open = index === active;
        const number = `0${index + 1}`;
        return (
          <li key={step.id} className={styles.panel} data-active={open || undefined}>
            <Image
              src={step.photo.src}
              alt={open ? step.photo.alt : ""}
              fill
              quality={85}
              sizes="(max-width: 900px) 100vw, 900px"
              style={{ objectPosition: step.photo.position }}
              className={styles.image}
            />
            <div className={styles.scrim} aria-hidden="true" />
            <button
              type="button"
              className={styles.trigger}
              aria-expanded={open}
              aria-controls={`selling-step-${step.id}`}
              onClick={() => setActive(index)}
            >
              <span className={styles.number}>{number}</span>
              <span className={styles.label}>{step.label}</span>
            </button>
            <div
              id={`selling-step-${step.id}`}
              className={styles.details}
              inert={!open}
              role="region"
              aria-label={step.label}
            >
              <h3 className={styles.title}>{step.label}</h3>
              <p className={styles.text}>{step.text}</p>
              <p className={styles.fieldsTitle}>{step.fieldsTitle}</p>
              <ul className={styles.fields}>
                {step.fields.map((field) => (
                  <li key={field}>{field}</li>
                ))}
              </ul>
              <div className={styles.footer}>
                <span>{step.note}</span>
                <Link
                  href="/sell"
                  prefetch={false}
                  tabIndex={open ? undefined : -1}
                  className={buttonClassName("primary", "sm")}
                >
                  Start selling my car
                </Link>
              </div>
            </div>
          </li>
        );
      })}
    </ol>
  );
}

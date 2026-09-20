"use client";

import { useEffect, useRef, useState } from "react";
import styles from "./AboutOdometer.module.css";

// Fictional figures for this demonstration site, counted to 1 December 2026. Yearly intake of 140 cars in 2014,
// rising to about 1,900 a year now, adds up to this total.
const YEAR_NOW = 2026;
const CARS_INSPECTED = 14680;
// Some cars fail inspection or are withdrawn, so fewer are sold than inspected.
const CARS_SOLD = 11920;

const DIGITS = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9];

function Odometer({ value, places, label }: { value: number; places: number; label: string }) {
  const digits = String(value).padStart(places, "0").split("").map(Number);
  return (
    <div className={styles.meter}>
      <span className={styles.meterLabel}>{label}</span>
      <div className={styles.drums} aria-hidden="true">
        {digits.map((digit, index) => (
          <span key={index} className={styles.drum}>
            <span
              className={styles.strip}
              style={{
                transform: `translateY(${-digit * 10}%)`,
                // The drums settle from the right, the way a real counter turns over.
                transitionDelay: `${(places - 1 - index) * 140}ms`,
              }}
            >
              {DIGITS.map((item) => (
                <span key={item}>{item}</span>
              ))}
            </span>
          </span>
        ))}
      </div>
    </div>
  );
}

// A decorative pair of odometers. They sit at zero until they scroll into view, then roll once to their final count.
export function AboutOdometer() {
  const root = useRef<HTMLDivElement>(null);
  const [counted, setCounted] = useState(false);

  useEffect(() => {
    const element = root.current;
    if (!element) {
      return;
    }
    if (window.matchMedia("(prefers-reduced-motion: reduce)").matches) {
      setCounted(true);
      return;
    }
    const observer = new IntersectionObserver(
      (entries) => {
        // Entries can arrive in a batch, so any one of them being in view is enough.
        if (entries.some((entry) => entry.isIntersecting)) {
          observer.disconnect();
          setCounted(true);
        }
      },
      { threshold: 0.4 },
    );
    observer.observe(element);
    return () => observer.disconnect();
  }, []);

  return (
    <section className={styles.bar} aria-label="ApexAuto in numbers">
      <div ref={root} className={styles.meters}>
        <Odometer value={counted ? YEAR_NOW : 0} places={4} label="Year" />
        <Odometer
          value={counted ? CARS_INSPECTED : 0}
          places={5}
          label="Cars inspected since 2014"
        />
        <Odometer value={counted ? CARS_SOLD : 0} places={5} label="Cars sold since 2014" />
      </div>
      <p className="sr-only">
        {CARS_INSPECTED.toLocaleString("en-MY")} cars inspected and{" "}
        {CARS_SOLD.toLocaleString("en-MY")} sold since 2014, counted to December {YEAR_NOW}.
      </p>
    </section>
  );
}

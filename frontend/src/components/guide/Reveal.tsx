"use client";

import { useEffect, useRef, useState, type ReactNode } from "react";
import { prefersReducedMotion } from "@/lib/motion";
import styles from "./Reveal.module.css";

type RevealState = "visible" | "hidden" | "shown";

// Where an element comes from as it appears: up from below, in from either side, or dropped from a slight tilt.
type RevealFrom = "up" | "left" | "right" | "drop";

// An element that eases into place the first time it scrolls into view. It renders visible, so the page reads
// without scripts and nothing flashes: only elements that start below the fold are hidden, once the page is
// running, and the browser's reduced-motion setting leaves everything as it is.
export function Reveal({
  as: Tag = "div",
  from = "up",
  delay = 0,
  className = "",
  children,
}: {
  as?: "div" | "li" | "header" | "span";
  from?: RevealFrom;
  delay?: number;
  className?: string;
  children: ReactNode;
}) {
  const ref = useRef<HTMLElement | null>(null);
  const [state, setState] = useState<RevealState>("visible");

  useEffect(() => {
    const element = ref.current;
    if (!element || prefersReducedMotion()) {
      return;
    }
    if (element.getBoundingClientRect().top < window.innerHeight * 0.9) {
      return;
    }
    setState("hidden");
    const observer = new IntersectionObserver(
      (entries) => {
        // Entries can arrive in a batch, so any one of them being in view is enough.
        if (entries.some((entry) => entry.isIntersecting)) {
          observer.disconnect();
          window.requestAnimationFrame(() => setState("shown"));
        }
      },
      { threshold: 0.15 },
    );
    observer.observe(element);
    return () => observer.disconnect();
  }, []);

  return (
    <Tag
      ref={(node: HTMLElement | null) => {
        ref.current = node;
      }}
      className={`${styles.reveal} ${className}`.trim()}
      data-reveal={state}
      data-from={from}
      style={{ ["--delay" as string]: `${delay}ms` }}
    >
      {children}
    </Tag>
  );
}

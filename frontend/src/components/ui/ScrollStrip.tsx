"use client";

import { useCallback, useLayoutEffect, useRef, useState, type ReactNode } from "react";
import { prefersReducedMotion } from "@/lib/motion";
import { Button } from "./Button";
import { Icon } from "./Icon";
import styles from "./ScrollStrip.module.css";

export function ScrollStrip({
  label,
  className = "",
  role = "group",
  children,
}: {
  label: string;
  className?: string;
  role?: "group" | "tablist";
  children: ReactNode;
}) {
  const frame = useRef<HTMLDivElement>(null);
  const viewport = useRef<HTMLDivElement>(null);
  const [edges, setEdges] = useState({ overflow: false, start: true, end: true });
  const measure = useCallback(() => {
    const strip = viewport.current;
    const host = frame.current;
    if (!strip || !host || host.clientWidth === 0) {
      return;
    }
    const next = {
      overflow: strip.scrollWidth > host.clientWidth + 1,
      start: strip.scrollLeft <= 1,
      end: strip.scrollLeft + strip.clientWidth >= strip.scrollWidth - 1,
    };
    setEdges((previous) =>
      previous.overflow === next.overflow &&
      previous.start === next.start &&
      previous.end === next.end
        ? previous
        : next,
    );
  }, []);

  useLayoutEffect(() => {
    const strip = viewport.current;
    const host = frame.current;
    if (!strip || !host) {
      return;
    }
    measure();
    const observer = new ResizeObserver(measure);
    observer.observe(host);
    observer.observe(strip);
    for (const child of strip.children) {
      observer.observe(child);
    }
    return () => observer.disconnect();
  }, [children, measure]);

  function scroll(direction: number) {
    const strip = viewport.current;
    if (!strip) {
      return;
    }
    strip.scrollBy({
      left: direction * strip.clientWidth * 0.75,
      behavior: prefersReducedMotion() ? "instant" : "smooth",
    });
  }

  // Option B of the owner's choice: a fade and a small round chevron appear only on the side that has more to show.
  return (
    <div ref={frame} className={styles.frame}>
      <div
        ref={viewport}
        role={role}
        aria-label={label}
        tabIndex={0}
        className={`${styles.viewport} ${className}`.trim()}
        onScroll={measure}
      >
        {children}
      </div>
      {edges.overflow && !edges.start && (
        <>
          <span aria-hidden="true" className={`${styles.fade} ${styles.fadeStart}`} />
          <Button
            size="icon"
            variant="ghost"
            className={`${styles.scrollControl} ${styles.controlStart}`}
            aria-label={`Scroll ${label.toLowerCase()} left`}
            onClick={() => scroll(-1)}
          >
            <Icon name="previous" grid={16} />
          </Button>
        </>
      )}
      {edges.overflow && !edges.end && (
        <>
          <span aria-hidden="true" className={`${styles.fade} ${styles.fadeEnd}`} />
          <Button
            size="icon"
            variant="ghost"
            className={`${styles.scrollControl} ${styles.controlEnd}`}
            aria-label={`Scroll ${label.toLowerCase()} right`}
            onClick={() => scroll(1)}
          >
            <Icon name="next" grid={16} />
          </Button>
        </>
      )}
    </div>
  );
}

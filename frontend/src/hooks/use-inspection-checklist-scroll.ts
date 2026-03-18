"use client";

import { useEffect, useRef, useState } from "react";

export type ScrollDirection = "above" | "below" | "collapsed";

export function useInspectionChecklistScroll(active: boolean, selectedCode: string) {
  const section = useRef<HTMLElement>(null);
  const tools = useRef<HTMLDivElement>(null);
  const selected = useRef<HTMLButtonElement>(null);
  const [hidden, setHidden] = useState(false);
  const [direction, setDirection] = useState<ScrollDirection | null>(null);

  useEffect(() => {
    const container = section.current;
    const header = tools.current;
    const point = selected.current;
    if (!active || !container || !header || !point) {
      return;
    }
    let frame = 0;

    function measure() {
      frame = 0;
      if (!container || !header || !point) {
        return;
      }
      const controls = header.getBoundingClientRect();
      const bounds = point.getBoundingClientRect();
      const collapsed = point.closest("details")?.open === false;
      const index = point.closest("nav")?.getBoundingClientRect();
      const body = container.parentElement?.getBoundingClientRect();
      container.style.setProperty("--checklist-tools-height", `${controls.height}px`);
      const top = Math.max(controls.bottom, index?.top ?? 0, 0);
      const bottom = Math.min(
        index?.bottom ?? innerHeight,
        body?.bottom ?? innerHeight,
        innerHeight,
      );
      const isAbove = bounds.bottom <= top;
      const isBelow = bounds.top >= bottom;
      const isHidden = collapsed || bounds.height === 0 || isAbove || isBelow;
      setHidden(isHidden);
      if (!isHidden) {
        setDirection(null);
      } else if (collapsed) {
        setDirection("collapsed");
      } else if (isAbove) {
        setDirection("above");
      } else if (isBelow) {
        setDirection("below");
      } else {
        setDirection(null);
      }
    }

    function schedule() {
      if (!frame) {
        frame = requestAnimationFrame(measure);
      }
    }

    const observer = new ResizeObserver(schedule);
    observer.observe(header);
    observer.observe(container);
    schedule();
    window.addEventListener("scroll", schedule, true);
    window.addEventListener("resize", schedule);
    container.addEventListener("toggle", schedule, true);
    return () => {
      observer.disconnect();
      cancelAnimationFrame(frame);
      window.removeEventListener("scroll", schedule, true);
      window.removeEventListener("resize", schedule);
      container.removeEventListener("toggle", schedule, true);
    };
  }, [active, selectedCode]);

  function showSelected() {
    const point = selected.current;
    if (!point) {
      return;
    }
    const category = point.closest("details");
    if (category) {
      category.open = true;
    }
    point.scrollIntoView({ block: "center", inline: "nearest", behavior: "smooth" });
    point.focus({ preventScroll: true });
  }

  return {
    sectionRef: section,
    toolsRef: tools,
    selectedRef: selected,
    selectedHidden: hidden,
    scrollDirection: direction,
    showSelected,
  };
}

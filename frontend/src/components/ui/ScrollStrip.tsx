"use client";

import {
  useCallback,
  useLayoutEffect,
  useRef,
  useState,
  type PointerEvent,
  type ReactNode,
} from "react";
import { prefersReducedMotion } from "@/lib/motion";
import { Button } from "./Button";
import { Icon } from "./Icon";
import styles from "./ScrollStrip.module.css";

const DRAG_THRESHOLD = 6;

export function ScrollStrip({
  label,
  className = "",
  role = "group",
  dragScroll = false,
  children,
}: {
  label: string;
  className?: string;
  role?: "group" | "tablist";
  // Lets a mouse drag the row sideways; touch and trackpads already scroll it natively.
  dragScroll?: boolean;
  children: ReactNode;
}) {
  const frame = useRef<HTMLDivElement>(null);
  const viewport = useRef<HTMLDivElement>(null);
  const [edges, setEdges] = useState({ overflow: false, start: true, end: true });
  const drag = useRef<{ x: number; left: number; moved: boolean; id: number } | null>(null);
  const suppressClick = useRef(false);
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

  function startDrag(event: PointerEvent<HTMLDivElement>) {
    const strip = viewport.current;
    if (!dragScroll || !strip || event.pointerType !== "mouse" || event.button !== 0) {
      return;
    }
    drag.current = { x: event.clientX, left: strip.scrollLeft, moved: false, id: event.pointerId };
  }

  function moveDrag(event: PointerEvent<HTMLDivElement>) {
    const strip = viewport.current;
    const current = drag.current;
    if (!strip || !current) {
      return;
    }
    const distance = event.clientX - current.x;
    if (!current.moved) {
      if (Math.abs(distance) < DRAG_THRESHOLD) {
        return;
      }
      current.moved = true;
      strip.setPointerCapture(current.id);
      strip.setAttribute("data-dragging", "");
    }
    strip.scrollLeft = current.left - distance;
  }

  function endDrag() {
    const strip = viewport.current;
    const current = drag.current;
    drag.current = null;
    if (!strip || !current?.moved) {
      return;
    }
    strip.removeAttribute("data-dragging");
    // The release of a drag must not also click whatever is under the pointer.
    suppressClick.current = true;
    setTimeout(() => {
      suppressClick.current = false;
    }, 0);
  }

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
        className={`${styles.viewport} ${dragScroll ? styles.draggable : ""} ${className}`.trim()}
        onScroll={measure}
        onPointerDown={startDrag}
        onPointerMove={moveDrag}
        onPointerUp={endDrag}
        onPointerCancel={endDrag}
        onDragStart={dragScroll ? (event) => event.preventDefault() : undefined}
        onClickCapture={(event) => {
          if (suppressClick.current) {
            event.preventDefault();
            event.stopPropagation();
          }
        }}
      >
        {children}
      </div>
      {edges.overflow && (
        <>
          <span
            aria-hidden="true"
            className={`${styles.fade} ${styles.fadeStart}`}
            data-hidden={edges.start || undefined}
          />
          <Button
            size="icon"
            variant="ghost"
            className={`${styles.scrollControl} ${styles.controlStart}`}
            data-hidden={edges.start || undefined}
            inert={edges.start || undefined}
            aria-label={`Scroll ${label.toLowerCase()} left`}
            onClick={() => scroll(-1)}
          >
            <Icon name="previous" />
          </Button>
          <span
            aria-hidden="true"
            className={`${styles.fade} ${styles.fadeEnd}`}
            data-hidden={edges.end || undefined}
          />
          <Button
            size="icon"
            variant="ghost"
            className={`${styles.scrollControl} ${styles.controlEnd}`}
            data-hidden={edges.end || undefined}
            inert={edges.end || undefined}
            aria-label={`Scroll ${label.toLowerCase()} right`}
            onClick={() => scroll(1)}
          >
            <Icon name="next" />
          </Button>
        </>
      )}
    </div>
  );
}

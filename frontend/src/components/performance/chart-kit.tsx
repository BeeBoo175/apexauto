"use client";

import { useSyncExternalStore } from "react";
import styles from "./Performance.module.css";

// Every chart on Performance and Home is drawn with Recharts and shares these settings, so scales, margins, height and
// hover behave the same on every tab and side by side charts line up.
// Chart marks that open a page or drawer change on hover and press, like other clickable controls.
export const linkedMarkClassName = styles.linkedMark;

export const chartHeight = 240;
export const axisWidth = 56;
export const chartMargin = { top: 12, right: 16, bottom: 4, left: 0 };
export const gridStroke = "var(--color-line-soft)";
export const axisStroke = "var(--color-line)";
export const colors = {
  blue: "#253c9a",
  green: "#0e6f5c",
  amber: "#b45309",
  violet: "#7c3aed",
};

export const tickStyle = { fill: "var(--color-muted)", fontSize: 12 };

const reducedMotionQuery = "(prefers-reduced-motion: reduce)";

function subscribe(listener: () => void) {
  const media = window.matchMedia(reducedMotionQuery);
  media.addEventListener("change", listener);
  return () => media.removeEventListener("change", listener);
}

// Charts draw in when they appear, unless the person asked their device for less motion.
export function useReducedMotion() {
  return useSyncExternalStore(
    subscribe,
    () => window.matchMedia(reducedMotionQuery).matches,
    () => false,
  );
}

interface TipLine {
  label?: string;
  value: string;
  color?: string;
}

// What a hover says: what the mark is, one line per figure and, when a click goes somewhere, where.
export interface TipData {
  title: string;
  lines: TipLine[];
  action?: string;
}

interface TipPayload {
  payload?: { tip?: TipData };
}

function TipContent({ active, payload }: { active?: boolean; payload?: TipPayload[] }) {
  const tip = payload?.[0]?.payload?.tip;
  if (!active || !tip) {
    return null;
  }
  return (
    <div className={styles.chartTip}>
      <strong>{tip.title}</strong>
      {tip.lines.map((line) => (
        <span key={`${line.label ?? ""}:${line.value}`} className={styles.chartTipLine}>
          {line.color && (
            <span
              className={styles.chartTipSwatch}
              style={{ background: line.color }}
              aria-hidden="true"
            />
          )}
          {line.label ? `${line.label}: ${line.value}` : line.value}
        </span>
      ))}
      {tip.action && <span className={styles.chartTipAction}>{tip.action}</span>}
    </div>
  );
}

// The tooltip settings every chart passes to Recharts. It may rise above the chart, so a tall bar never hides it, but
// it never leaves the chart sideways, so a mark at the edge of the screen keeps its card on screen.
export const tooltipSettings = {
  content: <TipContent />,
  isAnimationActive: false,
  offset: 18,
  allowEscapeViewBox: { x: false, y: true },
  wrapperStyle: { zIndex: 30, pointerEvents: "none" as const },
  cursor: { fill: "color-mix(in srgb, var(--color-line-soft) 60%, transparent)" },
};

"use client";

import type { ReactNode } from "react";
import { useRouter } from "next/navigation";
import {
  Bar,
  BarChart,
  Cell,
  LabelList,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";
import { chartMargin, colors, tooltipSettings, useReducedMotion, type TipData } from "./chart-kit";
import dashboard from "@/components/staff/DashboardSection.module.css";

interface BarItem {
  key: string;
  // Who or what the bar is about.
  label: string;
  // One quiet line the hover adds under the label, such as how long work usually waits.
  detail?: string;
  value: number | null;
  text: string;
  // The page that holds this item's work.
  href?: string;
  openLabel?: string;
}

// The bar labels start at the left edge of the section, like every other label on the page, instead of ending at the
// axis.
function LeftTick({ y, payload }: { y?: number; payload?: { value?: string } }) {
  return (
    <text
      x={0}
      y={y}
      dy={4}
      textAnchor="start"
      style={{ fill: "var(--color-graphite)", fontSize: 13 }}
    >
      {payload?.value}
    </text>
  );
}

const rowHeight = 44;
const labelWidth = 176;

// One measure as horizontal bars on a shared scale, the figure printed at each bar's end. Hover a bar for its detail;
// click it to open the page that holds that work.
export function MeasureBars({
  items,
  caption,
  empty,
  color = colors.blue,
}: {
  items: BarItem[];
  caption: string;
  empty: ReactNode;
  color?: string;
}) {
  const router = useRouter();
  const reduced = useReducedMotion();
  if (items.length === 0) {
    return <p className={dashboard.note}>{empty}</p>;
  }
  const data = items.map((item) => {
    const lines = [{ value: item.text }, ...(item.detail ? [{ value: item.detail }] : [])];
    const tip: TipData = {
      title: item.label,
      lines,
      action: item.href ? (item.openLabel ?? "Open the page for this") : undefined,
    };
    return { name: item.label, value: item.value ?? 0, text: item.text, href: item.href, tip };
  });
  return (
    <div role="img" aria-label={caption}>
      <ResponsiveContainer width="100%" height={items.length * rowHeight + 8}>
        <BarChart
          data={data}
          layout="vertical"
          margin={{ ...chartMargin, left: 0, right: 56 }}
          barCategoryGap={10}
        >
          <XAxis type="number" hide domain={[0, "auto"]} />
          <YAxis
            type="category"
            dataKey="name"
            width={labelWidth}
            tick={<LeftTick />}
            tickLine={false}
            axisLine={false}
          />
          <Tooltip {...tooltipSettings} />
          <Bar
            dataKey="value"
            fill={color}
            radius={[0, 2, 2, 0]}
            background={{ fill: "var(--color-canvas)", radius: 2 }}
            isAnimationActive={!reduced}
            animationDuration={600}
          >
            {data.map((entry) => (
              <Cell
                key={entry.name}
                style={{ cursor: entry.href ? "pointer" : "default" }}
                onClick={() => {
                  if (entry.href) {
                    router.push(entry.href);
                  }
                }}
              />
            ))}
            <LabelList
              dataKey="text"
              position="right"
              style={{ fill: "var(--color-graphite)", fontSize: 13, fontWeight: 700 }}
            />
          </Bar>
        </BarChart>
      </ResponsiveContainer>
    </div>
  );
}

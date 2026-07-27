"use client";

import { useRouter } from "next/navigation";
import {
  Bar,
  BarChart,
  CartesianGrid,
  Cell,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";
import {
  axisStroke,
  axisWidth,
  chartHeight,
  chartMargin,
  colors,
  gridStroke,
  tickStyle,
  tooltipSettings,
  useReducedMotion,
  type TipData,
} from "./chart-kit";

interface ChartColumn {
  key: string;
  label: string;
  value: number;
  // What a hover says about this column.
  title: string;
  lines: string[];
  // Where a click goes, with the words for it.
  href?: string;
  action?: string;
}

// Columns on a zero-based scale whose range and steps follow the data. Hover a column for its figures; click it to open
// the page that holds that work.
export function ColumnChart({
  columns,
  formatValue,
  label,
  color = colors.green,
  integer = false,
}: {
  columns: ChartColumn[];
  formatValue: (value: number) => string;
  label: string;
  color?: string;
  // Counts of people or things never take fractional steps.
  integer?: boolean;
}) {
  const router = useRouter();
  const reduced = useReducedMotion();
  const data = columns.map((column) => {
    const tip: TipData = {
      title: column.title,
      lines: column.lines.map((value) => ({ value })),
      action: column.href ? column.action : undefined,
    };
    return { name: column.label, value: column.value, href: column.href, tip };
  });
  return (
    <div role="img" aria-label={label}>
      <ResponsiveContainer width="100%" height={chartHeight}>
        <BarChart data={data} margin={chartMargin}>
          <CartesianGrid vertical={false} stroke={gridStroke} strokeDasharray="3 3" />
          <XAxis
            dataKey="name"
            tick={tickStyle}
            tickLine={false}
            axisLine={{ stroke: axisStroke }}
            interval="preserveStartEnd"
            minTickGap={12}
          />
          <YAxis
            width={axisWidth}
            tick={tickStyle}
            tickLine={false}
            axisLine={{ stroke: axisStroke }}
            allowDecimals={!integer}
            domain={[0, "auto"]}
            tickFormatter={(value: number) => formatValue(value)}
          />
          <Tooltip {...tooltipSettings} />
          <Bar
            dataKey="value"
            fill={color}
            radius={[2, 2, 0, 0]}
            maxBarSize={36}
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
          </Bar>
        </BarChart>
      </ResponsiveContainer>
    </div>
  );
}

"use client";

import { useRouter } from "next/navigation";
import {
  CartesianGrid,
  Line,
  LineChart as RechartsLineChart,
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
  gridStroke,
  tickStyle,
  tooltipSettings,
  useReducedMotion,
  type TipData,
} from "./ChartKit";

export interface LineSeries {
  key: string;
  label: string;
  color: string;
  values: number[];
  // Where a click on this line's points goes.
  href?: string;
  onSelect?: () => void;
}

// Lines over time. The axis range and steps follow the data, with zero as the floor so a line never overstates a
// change; hover any week for every line's figure, click a point to open what the line stands for.
export function LineChart({
  series,
  labels,
  formatValue,
  formatTick = formatValue,
  titleFor,
  label,
}: {
  series: LineSeries[];
  labels: string[];
  formatValue: (value: number) => string;
  // A shorter form for the scale, when the exact figure would not fit beside the axis.
  formatTick?: (value: number) => string;
  titleFor: (column: number) => string;
  label: string;
}) {
  const router = useRouter();
  const reduced = useReducedMotion();
  const data = labels.map((name, column) => {
    const tip: TipData = {
      title: titleFor(column),
      lines: series.map((line) => ({
        label: line.label,
        value: formatValue(line.values[column]),
        color: line.color,
      })),
    };
    const row: Record<string, unknown> = { name, tip };
    for (const line of series) {
      row[line.key] = line.values[column];
    }
    return row;
  });
  return (
    <div role="img" aria-label={label}>
      <ResponsiveContainer width="100%" height={chartHeight}>
        <RechartsLineChart data={data} margin={chartMargin}>
          <CartesianGrid vertical={false} stroke={gridStroke} strokeDasharray="3 3" />
          <XAxis
            dataKey="name"
            tick={tickStyle}
            tickLine={false}
            axisLine={{ stroke: axisStroke }}
            interval="preserveStartEnd"
            minTickGap={16}
          />
          <YAxis
            width={axisWidth}
            tick={tickStyle}
            tickLine={false}
            axisLine={{ stroke: axisStroke }}
            allowDecimals={false}
            domain={[0, "auto"]}
            tickFormatter={(value: number) => formatTick(value)}
          />
          <Tooltip {...tooltipSettings} />
          {series.map((line) => (
            <Line
              key={line.key}
              dataKey={line.key}
              stroke={line.color}
              strokeWidth={2}
              type="linear"
              dot={{ r: 3, fill: line.color, strokeWidth: 0 }}
              activeDot={{
                r: 6,
                style: { cursor: line.href || line.onSelect ? "pointer" : "default" },
                onClick: () => {
                  if (line.href) {
                    router.push(line.href);
                  }
                  line.onSelect?.();
                },
              }}
              isAnimationActive={!reduced}
              animationDuration={700}
            />
          ))}
        </RechartsLineChart>
      </ResponsiveContainer>
    </div>
  );
}

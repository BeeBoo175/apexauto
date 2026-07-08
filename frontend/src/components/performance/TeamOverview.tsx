"use client";

import { formatDays } from "@/lib/performance/format";
import type { TeamPoint } from "@/lib/performance/team";
import {
  CartesianGrid,
  ResponsiveContainer,
  Scatter,
  ScatterChart,
  Tooltip,
  XAxis,
  YAxis,
  ZAxis,
} from "recharts";
import { daysScale } from "@/lib/performance/axis";
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
} from "./ChartKit";
import { ColumnChart } from "./ColumnChart";
import { PerformanceSection, type SectionState } from "./PerformanceSection";
import { ChartSkeleton } from "./PerformanceSkeletons";
import type { Stat } from "./StatGrid";
import styles from "./Performance.module.css";

export interface OverviewLabels {
  // What the volume axis counts, for example "Cars delivered".
  volume: string;
  // What the days axis measures, for example "Days to deliver".
  days: string;
}

const average = (values: number[]) =>
  values.length === 0 ? null : values.reduce((sum, value) => sum + value, 0) / values.length;

// The team's headline figures, from every person in scope rather than the page on screen.
export function teamStats(points: TeamPoint[], total: number, labels: OverviewLabels): Stat[] {
  const volumeTotal = points.reduce((sum, point) => sum + point.volume, 0);
  const days = points.flatMap((point) => (point.days === null ? [] : [point.days]));
  return [
    { label: "People", value: String(total) },
    { label: labels.volume, value: String(volumeTotal) },
    {
      label: "Average per person",
      value: points.length === 0 ? "0" : (volumeTotal / points.length).toFixed(1),
    },
    { label: labels.days, value: formatDays(average(days)) },
  ];
}

// Bucket edges for the spread chart: single counts when the numbers are small, five even steps when they are not.
function buckets(maximum: number) {
  if (maximum <= 5) {
    return Array.from({ length: maximum + 1 }, (_, value) => ({
      from: value,
      to: value,
      label: String(value),
    }));
  }
  const width = Math.ceil((maximum + 1) / 5);
  return Array.from({ length: Math.ceil((maximum + 1) / width) }, (_, index) => {
    const from = index * width;
    const to = Math.min(maximum, from + width - 1);
    return { from, to, label: from === to ? String(from) : `${from} to ${to}` };
  });
}

// Two sections side by side, like the charts on Results, on the shared chart frame: every person as a dot, and how
// many people sit at each level of work. Hover a dot for the person's figures; click it to open their detail.
export function TeamCharts({
  points,
  labels,
  state,
  onOpen,
}: {
  points: TeamPoint[];
  labels: OverviewLabels;
  state: SectionState;
  onOpen: (point: TeamPoint) => void;
}) {
  if (state.hasData && (points.length === 0 || points.every((point) => point.volume === 0))) {
    return (
      <PerformanceSection
        title={`${labels.volume} per person`}
        state={state}
        placeholder={<ChartSkeleton />}
      >
        <p className={styles.note}>
          {points.length === 0
            ? "No one in this team to show."
            : `Nobody has ${labels.volume.toLowerCase()} in this period, so there is nothing to compare.`}
        </p>
      </PerformanceSection>
    );
  }
  return (
    <div className={styles.grid}>
      <PeopleScatter points={points} labels={labels} state={state} onOpen={onOpen} />
      <Spread points={points} labels={labels} state={state} />
    </div>
  );
}

function PeopleScatter({
  points,
  labels,
  state,
  onOpen,
}: {
  points: TeamPoint[];
  labels: OverviewLabels;
  state: SectionState;
  onOpen: (point: TeamPoint) => void;
}) {
  const reduced = useReducedMotion();
  const timed = points.filter((point) => point.days !== null);
  const untimed = points.length - timed.length;
  const scale = daysScale(timed.map((point) => point.days ?? 0));
  const data = timed.map((point) => ({
    x: point.volume,
    y: (point.days ?? 0) * scale.factor,
    point,
    tip: {
      title: point.name,
      lines: [
        { value: `${point.volume} ${labels.volume.toLowerCase()}` },
        { value: `${formatDays(point.days)} on average` },
      ],
      action: "Open their detail",
    } satisfies TipData,
  }));
  return (
    <PerformanceSection
      title={`${labels.volume} and ${labels.days.toLowerCase()}`}
      half
      aside={
        untimed > 0 && (
          <span className={styles.panelAside}>
            {untimed} without {labels.days.toLowerCase()}
          </span>
        )
      }
      state={state}
      placeholder={<ChartSkeleton />}
    >
      {timed.length === 0 ? (
        <p className={styles.note}>
          No one has a {labels.days.toLowerCase()} figure in this period yet.
        </p>
      ) : (
        <div
          role="img"
          aria-label={`${labels.volume} against ${labels.days.toLowerCase()}, per person`}
        >
          <ResponsiveContainer width="100%" height={chartHeight}>
            <ScatterChart margin={chartMargin}>
              <CartesianGrid stroke={gridStroke} strokeDasharray="3 3" />
              <XAxis
                type="number"
                dataKey="x"
                name={labels.volume}
                tick={tickStyle}
                tickLine={false}
                axisLine={{ stroke: axisStroke }}
                allowDecimals={false}
                domain={[0, "auto"]}
              />
              <YAxis
                type="number"
                dataKey="y"
                name={labels.days}
                width={axisWidth}
                tick={tickStyle}
                tickLine={false}
                axisLine={{ stroke: axisStroke }}
                domain={[0, "auto"]}
                tickFormatter={(value: number) => scale.format(value)}
              />
              <ZAxis range={[110, 110]} />
              <Tooltip {...tooltipSettings} cursor={{ strokeDasharray: "3 3" }} />
              <Scatter
                data={data}
                fill={colors.blue}
                isAnimationActive={!reduced}
                animationDuration={600}
                style={{ cursor: "pointer" }}
                onClick={(entry: { payload?: { point?: TeamPoint } }) => {
                  if (entry.payload?.point) {
                    onOpen(entry.payload.point);
                  }
                }}
              />
            </ScatterChart>
          </ResponsiveContainer>
        </div>
      )}
    </PerformanceSection>
  );
}

function Spread({
  points,
  labels,
  state,
}: {
  points: TeamPoint[];
  labels: OverviewLabels;
  state: SectionState;
}) {
  const maxVolume = Math.max(0, ...points.map((point) => point.volume));
  const groups = buckets(maxVolume).map((bucket) => ({
    ...bucket,
    count: points.filter((point) => point.volume >= bucket.from && point.volume <= bucket.to)
      .length,
  }));
  return (
    <PerformanceSection
      title={`People by ${labels.volume.toLowerCase()}`}
      half
      state={state}
      placeholder={<ChartSkeleton />}
    >
      <ColumnChart
        label={`People by ${labels.volume.toLowerCase()}`}
        formatValue={String}
        integer
        columns={groups.map((group) => ({
          key: group.label,
          label: group.label,
          value: group.count,
          title: `${group.label} ${labels.volume.toLowerCase()}`,
          lines: [`${group.count} ${group.count === 1 ? "person" : "people"}`],
        }))}
      />
    </PerformanceSection>
  );
}

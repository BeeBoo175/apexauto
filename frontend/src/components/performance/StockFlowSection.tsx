import type { CSSProperties } from "react";
import { weekLabel } from "@/lib/performance/format";
import type { PerformanceStockFlow, StockWeek } from "@/lib/performance/performance";
import { PerformanceSection, type SectionState } from "./PerformanceSection";
import { ChartSkeleton } from "./PerformanceSkeletons";
import styles from "./Performance.module.css";

// Each week keeps at least this width, so a long period scrolls sideways instead of crowding its labels.
const weekMinimum = 32;
// Each week's figures print beneath it while they fit; longer periods show them on hover and in the screen-reader
// table instead.
const figureWeeks = 6;
// Room above the highest point, as a share of the plot height.
const headroom = 0.15;

const series = [
  { key: "checkIns", label: "Checked in", className: styles.seriesIn },
  { key: "deliveries", label: "Delivered", className: styles.seriesOut },
] as const;

// The weekly flow of vehicles across the full width: the two lines show direction and each week prints its own
// figures beneath, so the chart needs no table beside it. Screen readers get the same figures as a table.
export function StockFlowSection({
  flow,
  state,
}: {
  flow: PerformanceStockFlow | null;
  state: SectionState;
}) {
  return (
    <PerformanceSection
      title="Vehicles in and out, by week"
      half
      aside={
        <span className={styles.flowAside}>
          {flow && (
            <span className={styles.panelAside}>
              {total(flow.weeks, "checkIns")} in, {total(flow.weeks, "deliveries")} out
            </span>
          )}
          <SeriesLegend />
        </span>
      }
      state={{ ...state, hasData: flow !== null }}
      placeholder={<ChartSkeleton />}
    >
      {flow && (
        <>
          <StockChart flow={flow} />
          <div className="sr-only">
            <StockTable flow={flow} />
          </div>
        </>
      )}
    </PerformanceSection>
  );
}

function SeriesLegend() {
  return (
    <ul className={styles.legend} aria-label="Lines">
      {series.map((line) => (
        <li key={line.key}>
          <span className={`${styles.swatch} ${line.className}`} aria-hidden="true" />
          {line.label}
        </li>
      ))}
    </ul>
  );
}

function total(weeks: StockWeek[], key: "checkIns" | "deliveries") {
  return weeks.reduce((sum, week) => sum + week[key], 0);
}

// Fewer week labels as the period grows, so they stay readable.
function labelEvery(weeks: number) {
  if (weeks > 16) {
    return 4;
  }
  return weeks > 8 ? 2 : 1;
}

// Points sit at the centre of their week column, in percent of the plot, so the plot fills any width.
function pointsFor(weeks: StockWeek[], key: "checkIns" | "deliveries", maximum: number) {
  return weeks.map((week, index) => ({
    x: ((index + 0.5) / weeks.length) * 100,
    y: 100 - (week[key] / maximum) * (1 - headroom) * 100,
  }));
}

function StockChart({ flow }: { flow: PerformanceStockFlow }) {
  if (flow.weeks.length === 0) {
    return <p className={styles.note}>No weeks in this period.</p>;
  }
  const maximum = Math.max(1, ...flow.weeks.flatMap((week) => [week.checkIns, week.deliveries]));
  const every = labelEvery(flow.weeks.length);
  const layout: CSSProperties = {
    gridTemplateColumns: `repeat(${flow.weeks.length}, minmax(${weekMinimum}px, 1fr))`,
    minWidth: flow.weeks.length * weekMinimum,
  };
  return (
    <div className={styles.scroll}>
      <div className={styles.flow} style={layout} aria-hidden="true">
        <div className={styles.plot}>
          <svg className={styles.lines} viewBox="0 0 100 100" preserveAspectRatio="none">
            {series.map((line) => (
              <polyline
                key={line.key}
                className={`${styles.line} ${line.className}`}
                points={pointsFor(flow.weeks, line.key, maximum)
                  .map((point) => `${point.x},${point.y}`)
                  .join(" ")}
              />
            ))}
          </svg>
          {series.map((line) =>
            pointsFor(flow.weeks, line.key, maximum).map((point, index) => (
              <span
                key={`${line.key}:${point.x}`}
                className={`${styles.point} ${line.className}`}
                style={{ left: `${point.x}%`, top: `${point.y}%` }}
                title={`Week of ${weekLabel(flow.weeks[index].weekStart)}: ${flow.weeks[index][line.key]} ${line.label.toLowerCase()}`}
              />
            )),
          )}
        </div>
        {flow.weeks.map((week, index) => (
          <span key={week.weekStart} className={styles.flowWeek}>
            <span>{index % every === 0 ? weekLabel(week.weekStart) : ""}</span>
            {flow.weeks.length <= figureWeeks && (
              <span className={styles.flowFigures}>
                {week.checkIns} in, {week.deliveries} out
              </span>
            )}
          </span>
        ))}
      </div>
    </div>
  );
}

function StockTable({ flow }: { flow: PerformanceStockFlow }) {
  return (
    <div className={styles.scroll}>
      <table className={styles.table}>
        <caption className="sr-only">Weekly vehicles checked in and delivered</caption>
        <thead>
          <tr>
            <th scope="col">Week starting</th>
            <th scope="col" className={styles.number}>
              Checked in
            </th>
            <th scope="col" className={styles.number}>
              Delivered
            </th>
          </tr>
        </thead>
        <tbody>
          {flow.weeks.map((week) => (
            <tr key={week.weekStart}>
              <th scope="row">{weekLabel(week.weekStart)}</th>
              <td className={styles.number}>{week.checkIns}</td>
              <td className={styles.number}>{week.deliveries}</td>
            </tr>
          ))}
        </tbody>
        <tfoot>
          <tr>
            <th scope="row">Period total</th>
            <td className={styles.number}>{total(flow.weeks, "checkIns")}</td>
            <td className={styles.number}>{total(flow.weeks, "deliveries")}</td>
          </tr>
        </tfoot>
      </table>
    </div>
  );
}

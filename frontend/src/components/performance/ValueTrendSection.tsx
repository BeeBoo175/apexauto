import type { CSSProperties } from "react";
import { formatCurrency } from "@/lib/currency-format";
import { weekLabel } from "@/lib/performance/format";
import type { PerformanceStockFlow } from "@/lib/performance/performance";
import { PerformanceSection, type SectionState } from "./PerformanceSection";
import { ChartSkeleton } from "./PerformanceSkeletons";
import styles from "./Performance.module.css";

const weekMinimum = 32;

// Short MYR amounts for the column tops, such as "MYR 1.2M"; the full amounts are in the accessible table.
const compact = new Intl.NumberFormat("en-MY", {
  style: "currency",
  currency: "MYR",
  notation: "compact",
  maximumFractionDigits: 1,
});

// The agreed value delivered each week of the period, as columns on one scale with each week's amount above it.
export function ValueTrendSection({
  flow,
  state,
}: {
  flow: PerformanceStockFlow | null;
  state: SectionState;
}) {
  const total = flow?.weeks.reduce((sum, week) => sum + week.deliveredValue, 0) ?? 0;
  return (
    <PerformanceSection
      title="Delivered value, by week"
      half
      aside={
        flow && <span className={styles.panelAside}>{formatCurrency(total)} in the period</span>
      }
      state={{ ...state, hasData: flow !== null }}
      placeholder={<ChartSkeleton />}
    >
      {flow && <ValueColumns flow={flow} />}
    </PerformanceSection>
  );
}

function ValueColumns({ flow }: { flow: PerformanceStockFlow }) {
  if (flow.weeks.length === 0) {
    return <p className={styles.note}>No weeks in this period.</p>;
  }
  const maximum = Math.max(1, ...flow.weeks.map((week) => week.deliveredValue));
  if (flow.weeks.every((week) => week.deliveredValue === 0)) {
    return <p className={styles.note}>No sales were delivered in this period.</p>;
  }
  const every = flow.weeks.length > 8 ? 2 : 1;
  const layout: CSSProperties = {
    gridTemplateColumns: `repeat(${flow.weeks.length}, minmax(${weekMinimum}px, 1fr))`,
    minWidth: flow.weeks.length * weekMinimum,
  };
  return (
    <div className={styles.scroll}>
      <div className={styles.columns} style={layout} aria-hidden="true">
        {flow.weeks.map((week) => (
          <span
            key={week.weekStart}
            className={styles.column}
            title={`Week of ${weekLabel(week.weekStart)}: ${formatCurrency(week.deliveredValue)}`}
          >
            <span className={styles.columnValue}>
              {flow.weeks.length <= 8 && week.deliveredValue > 0
                ? compact.format(week.deliveredValue)
                : ""}
            </span>
            <span className={styles.columnTrack}>
              <span
                className={styles.columnBar}
                style={{ height: `${(week.deliveredValue / maximum) * 100}%` }}
              />
            </span>
          </span>
        ))}
        {flow.weeks.map((week, index) => (
          <span key={week.weekStart} className={styles.flowWeek}>
            {index % every === 0 ? weekLabel(week.weekStart) : ""}
          </span>
        ))}
      </div>
      <table className="sr-only">
        <caption>Agreed value delivered each week</caption>
        <thead>
          <tr>
            <th scope="col">Week starting</th>
            <th scope="col">Delivered value</th>
          </tr>
        </thead>
        <tbody>
          {flow.weeks.map((week) => (
            <tr key={week.weekStart}>
              <th scope="row">{weekLabel(week.weekStart)}</th>
              <td>{formatCurrency(week.deliveredValue)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

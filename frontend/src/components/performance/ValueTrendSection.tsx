import { formatCurrency } from "@/lib/currency-format";
import { weekLabel } from "@/lib/performance/format";
import type { PerformanceStockFlow } from "@/lib/performance/performance";
import { ColumnChart } from "./ColumnChart";
import { PerformanceSection, type SectionState } from "./PerformanceSection";
import { ChartSkeleton } from "./PerformanceSkeletons";
import styles from "./Performance.module.css";

// Short MYR amounts for the scale, such as "MYR 1.2M"; the full amounts are in the hover and the accessible table.
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
  if (flow.weeks.every((week) => week.deliveredValue === 0)) {
    return <p className={styles.note}>No sales were delivered in this period.</p>;
  }
  return (
    <>
      <ColumnChart
        label="Delivered value by week"
        formatValue={(value) => compact.format(value)}
        columns={flow.weeks.map((week) => ({
          key: week.weekStart,
          label: weekLabel(week.weekStart),
          value: week.deliveredValue,
          title: `Week of ${weekLabel(week.weekStart)}`,
          lines: [`${formatCurrency(week.deliveredValue)} delivered`],
          href: "/dashboard/sales",
          action: "Open Sales",
        }))}
      />
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
    </>
  );
}

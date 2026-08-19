import { weekLabel } from "@/lib/performance/format";
import type { PerformanceStockFlow, StockWeek } from "@/lib/performance/performance";
import { colors } from "./chart-kit";
import { EmptyChart } from "./EmptyChart";
import { Legend } from "./Legend";
import { LineChart } from "./LineChart";
import { DashboardSection, type SectionState } from "@/components/staff/DashboardSection";
import { ChartSkeleton } from "./PerformanceSkeletons";
import styles from "./Performance.module.css";
import dashboard from "@/components/staff/DashboardSection.module.css";

const series = [
  { key: "checkIns", label: "Checked in", color: colors.blue, href: "/dashboard/intake" },
  { key: "deliveries", label: "Delivered", color: colors.green, href: "/dashboard/sales" },
] as const;

// The weekly flow of vehicles: two lines on a scale that follows the data, hover a week for both figures and click a
// point to open the page behind it. Screen readers get the same figures as a table.
export function StockFlowSection({
  flow,
  state,
}: {
  flow: PerformanceStockFlow | null;
  state: SectionState;
}) {
  return (
    <DashboardSection
      title="Vehicles in and out"
      half
      aside={
        <span className={styles.flowAside}>
          {flow && (
            <span className={dashboard.panelAside}>
              {total(flow.weeks, "checkIns")} in, {total(flow.weeks, "deliveries")} out
            </span>
          )}
          <Legend
            label="Lines"
            items={series.map((line) => ({
              key: line.key,
              label: line.label,
              color: line.color,
            }))}
          />
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
    </DashboardSection>
  );
}

function total(weeks: StockWeek[], key: "checkIns" | "deliveries") {
  return weeks.reduce((sum, week) => sum + week[key], 0);
}

function StockChart({ flow }: { flow: PerformanceStockFlow }) {
  if (
    flow.weeks.length === 0 ||
    flow.weeks.every((week) => week.checkIns + week.deliveries === 0)
  ) {
    return (
      <EmptyChart art="line" icon="car" title="No vehicles moved">
        Check-ins and deliveries in this period appear here.
      </EmptyChart>
    );
  }
  return (
    <LineChart
      label="Vehicles checked in and delivered each week"
      series={series.map((line) => ({
        key: line.key,
        label: line.label,
        color: line.color,
        href: line.href,
        values: flow.weeks.map((week) => week[line.key]),
      }))}
      labels={flow.weeks.map((week) => weekLabel(week.weekStart))}
      formatValue={String}
      titleFor={(column) => `Week of ${weekLabel(flow.weeks[column].weekStart)}`}
    />
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

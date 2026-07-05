import { weekLabel } from "@/lib/performance/format";
import type { PerformanceStockFlow } from "@/lib/performance/performance";
import { PerformanceSection, type SectionState } from "./PerformanceSection";
import styles from "./Performance.module.css";

const stride = 36;
const barWidth = 14;
const chartHeight = 140;
const labelHeight = 20;

// Fewer week labels as the period grows, so they stay readable.
function labelEvery(weeks: number) {
  if (weeks > 20) {
    return 4;
  }
  return weeks > 10 ? 2 : 1;
}

export function StockFlowSection({
  flow,
  state,
  asTable,
}: {
  flow: PerformanceStockFlow | null;
  state: SectionState;
  asTable: boolean;
}) {
  return (
    <PerformanceSection title="Stock in and out" state={state}>
      {flow && (asTable ? <StockTable flow={flow} /> : <StockChart flow={flow} />)}
    </PerformanceSection>
  );
}

function StockChart({ flow }: { flow: PerformanceStockFlow }) {
  const maximum = Math.max(1, ...flow.weeks.flatMap((week) => [week.checkIns, week.deliveries]));
  const width = Math.max(flow.weeks.length * stride, stride);
  const scale = (value: number) => (value / maximum) * (chartHeight - 8);
  const every = labelEvery(flow.weeks.length);
  const totalIn = flow.weeks.reduce((sum, week) => sum + week.checkIns, 0);
  const totalOut = flow.weeks.reduce((sum, week) => sum + week.deliveries, 0);
  return (
    <>
      <div className={styles.scroll}>
        <svg
          className={styles.chart}
          width={width}
          height={chartHeight + labelHeight}
          viewBox={`0 0 ${width} ${chartHeight + labelHeight}`}
          role="img"
          aria-label={`Weekly vehicles checked in and delivered. ${totalIn} checked in and ${totalOut} delivered in the period.`}
        >
          {flow.weeks.map((week, index) => {
            const x = index * stride + (stride - barWidth * 2 - 2) / 2;
            return (
              <g key={week.weekStart}>
                <rect
                  x={x}
                  y={chartHeight - scale(week.checkIns)}
                  width={barWidth}
                  height={scale(week.checkIns)}
                  fill="var(--color-graphite-soft)"
                >
                  <title>{`Week of ${weekLabel(week.weekStart)}: ${week.checkIns} checked in`}</title>
                </rect>
                <rect
                  x={x + barWidth + 2}
                  y={chartHeight - scale(week.deliveries)}
                  width={barWidth}
                  height={scale(week.deliveries)}
                  fill="var(--color-trust)"
                >
                  <title>{`Week of ${weekLabel(week.weekStart)}: ${week.deliveries} delivered`}</title>
                </rect>
                {index % every === 0 && (
                  <text
                    className={styles.chartLabel}
                    x={index * stride + stride / 2}
                    y={chartHeight + 14}
                    textAnchor="middle"
                  >
                    {weekLabel(week.weekStart)}
                  </text>
                )}
              </g>
            );
          })}
          <line
            x1={0}
            x2={width}
            y1={chartHeight}
            y2={chartHeight}
            stroke="var(--color-line)"
            strokeWidth={1}
          />
        </svg>
      </div>
      <ul className={styles.legend}>
        <li>
          <span className={styles.swatch} style={{ background: "var(--color-graphite-soft)" }} />
          Checked in ({totalIn})
        </li>
        <li>
          <span className={styles.swatch} style={{ background: "var(--color-trust)" }} />
          Delivered ({totalOut})
        </li>
        <li>Weeks start on Monday, Malaysia time</li>
      </ul>
    </>
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
      </table>
    </div>
  );
}

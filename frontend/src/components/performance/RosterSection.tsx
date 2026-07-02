import { hourLabel, weekdayLabels } from "@/lib/performance/format";
import type { PerformanceRoster } from "@/lib/performance/performance";
import { PerformanceSection, type SectionState } from "./PerformanceSection";
import styles from "./Performance.module.css";

export function RosterSection({
  roster,
  state,
  asTable,
}: {
  roster: PerformanceRoster | null;
  state: SectionState;
  asTable: boolean;
}) {
  return (
    <PerformanceSection title="Rostering" state={state}>
      {roster && (asTable ? <RosterTable roster={roster} /> : <RosterHeatmap roster={roster} />)}
    </PerformanceSection>
  );
}

function RosterHeatmap({ roster }: { roster: PerformanceRoster }) {
  if (roster.cells.length === 0) {
    return <p className={styles.note}>No bookings in this period.</p>;
  }
  const first = Math.min(...roster.cells.map((cell) => cell.hour));
  const last = Math.max(...roster.cells.map((cell) => cell.hour));
  const hours = Array.from({ length: last - first + 1 }, (_, index) => first + index);
  const counts = new Map(
    roster.cells.map((cell) => [`${cell.weekday}:${cell.hour}`, cell] as const),
  );
  const maximum = Math.max(1, ...roster.cells.map((cell) => cell.appointments + cell.testDrives));
  const columns = `56px repeat(${hours.length}, minmax(28px, 1fr))`;
  return (
    <>
      <div
        className={styles.heat}
        role="table"
        aria-label="Booked appointments and test drives by weekday and hour"
      >
        <div className={styles.heatRow} style={{ gridTemplateColumns: columns }} role="row">
          <span role="columnheader" className="sr-only">
            Day
          </span>
          {hours.map((hour) => (
            <span key={hour} className={styles.heatHead} role="columnheader">
              {hourLabel(hour)}
            </span>
          ))}
        </div>
        {weekdayLabels.map((day, weekday) => (
          <div
            key={day}
            className={styles.heatRow}
            style={{ gridTemplateColumns: columns }}
            role="row"
          >
            <span className={styles.heatDay} role="rowheader">
              {day}
            </span>
            {hours.map((hour) => {
              const cell = counts.get(`${weekday}:${hour}`);
              const total = cell ? cell.appointments + cell.testDrives : 0;
              const share = total / maximum;
              return (
                <span
                  key={hour}
                  role="cell"
                  className={styles.heatCell}
                  data-strong={share > 0.55}
                  style={{ "--heat": share } as React.CSSProperties}
                  title={`${day} ${hourLabel(hour)}: ${cell?.appointments ?? 0} appointments, ${cell?.testDrives ?? 0} test drives`}
                >
                  {total > 0 ? total : ""}
                </span>
              );
            })}
          </div>
        ))}
      </div>
      <p className={styles.note}>
        Appointments and test drives booked in the period, by start time. Cancelled bookings are
        left out.
      </p>
    </>
  );
}

function RosterTable({ roster }: { roster: PerformanceRoster }) {
  return (
    <div className={styles.scroll}>
      <table className={styles.table}>
        <caption className="sr-only">
          Booked appointments and test drives by weekday and hour
        </caption>
        <thead>
          <tr>
            <th scope="col">Day</th>
            <th scope="col">Hour</th>
            <th scope="col" className={styles.number}>
              Appointments
            </th>
            <th scope="col" className={styles.number}>
              Test drives
            </th>
          </tr>
        </thead>
        <tbody>
          {roster.cells.map((cell) => (
            <tr key={`${cell.weekday}:${cell.hour}`}>
              <th scope="row">{weekdayLabels[cell.weekday]}</th>
              <td>{hourLabel(cell.hour)}</td>
              <td className={styles.number}>{cell.appointments}</td>
              <td className={styles.number}>{cell.testDrives}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

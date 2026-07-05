import { hourLabel, weekdayLabels } from "@/lib/performance/format";
import type { PerformanceRoster } from "@/lib/performance/performance";
import { PerformanceSection, type SectionState } from "./PerformanceSection";
import { HeatSkeleton } from "./PerformanceSkeletons";
import styles from "./Performance.module.css";

// Booked Intake and test drives over the next four weeks by weekday and hour, to see when more people are needed.
// Every booked cell prints its count; the cell title splits it into appointments and test drives.
export function RosterSection({
  roster,
  state,
  half = false,
}: {
  roster: PerformanceRoster | null;
  state: SectionState;
  half?: boolean;
}) {
  return (
    <PerformanceSection
      title="Bookings, next 4 weeks"
      half={half}
      aside={<HeatLegend />}
      state={{ ...state, hasData: roster !== null }}
      placeholder={<HeatSkeleton />}
    >
      {roster && <RosterHeatmap roster={roster} />}
    </PerformanceSection>
  );
}

function HeatLegend() {
  return (
    <ul className={styles.legend} aria-label="Shading">
      <li>
        <span className={`${styles.swatch} ${styles.swatchEmpty}`} aria-hidden="true" />
        Nothing booked
      </li>
      <li>
        <span className={`${styles.swatch} ${styles.swatchLow}`} aria-hidden="true" />
        Fewer bookings
      </li>
      <li>
        <span className={`${styles.swatch} ${styles.swatchHigh}`} aria-hidden="true" />
        More bookings
      </li>
    </ul>
  );
}

function RosterHeatmap({ roster }: { roster: PerformanceRoster }) {
  if (roster.cells.length === 0) {
    return <p className={styles.note}>Nothing is booked in this window.</p>;
  }
  const first = Math.min(...roster.cells.map((cell) => cell.hour));
  const last = Math.max(...roster.cells.map((cell) => cell.hour));
  const hours = Array.from({ length: last - first + 1 }, (_, index) => first + index);
  const counts = new Map(
    roster.cells.map((cell) => [`${cell.weekday}:${cell.hour}`, cell] as const),
  );
  const maximum = Math.max(1, ...roster.cells.map((cell) => cell.appointments + cell.testDrives));
  const columns = `56px repeat(${hours.length}, minmax(32px, 1fr))`;
  return (
    <div
      className={styles.heat}
      role="table"
      aria-label="Booked appointments and test drives by weekday and hour"
    >
      <div className={styles.heatRow} style={{ gridTemplateColumns: columns }} role="row">
        <span role="columnheader" className={styles.heatDay}>
          <span className="sr-only">Day</span>
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
                data-empty={total === 0}
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
  );
}

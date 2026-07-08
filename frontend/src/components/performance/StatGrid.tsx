import type { ReactNode } from "react";
import type { Delta } from "@/lib/performance/format";
import styles from "./Performance.module.css";

export interface Stat {
  label: string;
  value: string;
  unit?: string;
  // The quiet line under the figure: a change on the period before, or what the figure covers.
  note?: Delta | string;
}

function noteClass(note: Delta | string) {
  if (typeof note === "string" || note.tone === "neutral") {
    return styles.delta;
  }
  return `${styles.delta} ${note.tone === "success" ? styles.deltaUp : styles.deltaDown}`;
}

// The figures a manager reads first, in one row: the same layout for the headline, the team and anywhere else a
// page opens with a few numbers.
export function StatGrid({ stats }: { stats: Stat[] }): ReactNode {
  return (
    <dl className={styles.stats}>
      {stats.map((stat) => (
        <div key={stat.label} className={styles.stat}>
          <dt className={styles.statLabel}>{stat.label}</dt>
          <dd className={styles.statValue}>
            {stat.value}
            {stat.unit && <span className={styles.statUnit}> {stat.unit}</span>}
          </dd>
          {stat.note && (
            <dd className={noteClass(stat.note)}>
              {typeof stat.note === "string" ? stat.note : stat.note.text}
            </dd>
          )}
        </div>
      ))}
    </dl>
  );
}

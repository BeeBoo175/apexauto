import styles from "./Performance.module.css";
import dashboard from "@/components/staff/DashboardSection.module.css";

// Placeholders built from the same elements and classes as the loaded content, so a section has its final height from
// the first paint and nothing moves when its figures arrive.

export function StatsSkeleton() {
  return (
    <div className={styles.stats}>
      {[0, 1, 2, 3].map((index) => (
        <div key={index} className={styles.stat}>
          <span
            className={`${dashboard.placeholder} ${styles.shellStat} ${dashboard.shellNarrow}`}
          />
          <span className={`${dashboard.placeholder} ${styles.shellStatValue}`} />
          <span className={`${dashboard.placeholder} ${styles.shellStat}`} />
        </div>
      ))}
    </div>
  );
}

export function ChartSkeleton() {
  return <span className={`${dashboard.placeholder} ${styles.shellChart}`} />;
}

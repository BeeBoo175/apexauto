import styles from "./Performance.module.css";

// Placeholders built from the same elements and classes as the loaded content, so a section has its final height from
// the first paint and nothing moves when its figures arrive.

export function RowsSkeleton({ count, tall = false }: { count: number; tall?: boolean }) {
  return (
    <div className={styles.rows}>
      {Array.from({ length: count }, (_, index) => (
        <div key={index} className={styles.row}>
          <div className={`${styles.rowHead} ${tall ? styles.rowTall : ""}`.trim()}>
            <span className={`${styles.placeholder} ${styles.shellLine} ${styles.shellWide}`} />
          </div>
          <span className={`${styles.track} ${styles.placeholder} ${styles.shellBar}`} />
          <span className={styles.rowDetail}>
            <span className={`${styles.placeholder} ${styles.shellLine} ${styles.shellNarrow}`} />
          </span>
        </div>
      ))}
    </div>
  );
}

export function StatsSkeleton() {
  return (
    <div className={styles.stats}>
      {[0, 1, 2, 3].map((index) => (
        <div key={index} className={styles.stat}>
          <span className={`${styles.placeholder} ${styles.shellStat} ${styles.shellNarrow}`} />
          <span className={`${styles.placeholder} ${styles.shellStatValue}`} />
          <span className={`${styles.placeholder} ${styles.shellStat}`} />
        </div>
      ))}
    </div>
  );
}

export function ChartSkeleton() {
  return <span className={`${styles.placeholder} ${styles.shellChart}`} />;
}

export function HeatSkeleton() {
  return (
    <div className={styles.heat}>
      <span className={`${styles.placeholder} ${styles.shellHeatHead}`} />
      {Array.from({ length: 7 }, (_, index) => (
        <span key={index} className={`${styles.placeholder} ${styles.shellHeat}`} />
      ))}
    </div>
  );
}

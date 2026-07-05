import type { CSSProperties } from "react";
import performance from "@/components/performance/Performance.module.css";
import { RowsSkeleton } from "@/components/performance/PerformanceSkeletons";
import styles from "./Home.module.css";

// The first paint of Home, built from the same frame classes as the loaded page: the filter toolbar for the role
// that has one, the band with the role's number of tiles and the two panes. The page reveals into this geometry.
export function HomeLoadingShell({ tiles, toolbar }: { tiles: number; toolbar: boolean }) {
  return (
    <div className={performance.workspace} aria-hidden="true">
      {toolbar && (
        <div className={performance.shellToolbar}>
          <span className={`${performance.placeholder} ${performance.shellControl}`} />
        </div>
      )}
      <div className={styles.view}>
        <div className={styles.band}>
          <span className={`${performance.placeholder} ${performance.shellTitle}`} />
          <div className={styles.tiles} style={{ "--tiles": tiles } as CSSProperties}>
            {Array.from({ length: tiles }, (_, index) => (
              <div key={index} className={styles.tile}>
                <div className={styles.tileHead}>
                  <span className={`${performance.placeholder} ${styles.tileIcon}`} />
                  <span className={`${performance.placeholder} ${performance.shellTitle}`} />
                </div>
                <span className={`${performance.placeholder} ${styles.shellTile}`} />
              </div>
            ))}
          </div>
        </div>
        <div className={styles.panes}>
          <div className={styles.pane}>
            <div className={performance.panel}>
              <span className={`${performance.placeholder} ${performance.shellTitle}`} />
              <span className={`${performance.placeholder} ${styles.shellTimeline}`} />
            </div>
          </div>
          <div className={styles.pane}>
            <div className={performance.panel}>
              <span className={`${performance.placeholder} ${performance.shellTitle}`} />
              <RowsSkeleton count={5} tall />
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}

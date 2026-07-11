import type { CSSProperties } from "react";
import dashboard from "@/components/staff/DashboardSection.module.css";
import { RowsSkeleton } from "@/components/staff/DashboardSkeletons";
import styles from "./Home.module.css";

// The first paint of Home, built from the same frame classes as the loaded page: the filter toolbar for the role
// that has one, the band with the role's number of tiles and the two panes. The page reveals into this geometry.
export function HomeLoadingShell({ tiles, toolbar }: { tiles: number; toolbar: boolean }) {
  return (
    <div className={dashboard.workspace} aria-hidden="true">
      {toolbar && (
        <div className={dashboard.shellToolbar}>
          <span className={`${dashboard.placeholder} ${dashboard.shellControl}`} />
        </div>
      )}
      <div className={styles.view}>
        <div className={styles.band}>
          <span className={`${dashboard.placeholder} ${dashboard.shellTitle}`} />
          <div className={styles.tiles} style={{ "--tiles": tiles } as CSSProperties}>
            {Array.from({ length: tiles }, (_, index) => (
              <div key={index} className={styles.tile}>
                <div className={styles.tileHead}>
                  <span className={`${dashboard.placeholder} ${styles.tileIcon}`} />
                  <span className={`${dashboard.placeholder} ${dashboard.shellTitle}`} />
                </div>
                <span className={`${dashboard.placeholder} ${styles.shellTile}`} />
              </div>
            ))}
          </div>
        </div>
        <div className={styles.panes}>
          <div className={styles.pane}>
            <div className={dashboard.panel}>
              <span className={`${dashboard.placeholder} ${dashboard.shellTitle}`} />
              <span className={`${dashboard.placeholder} ${styles.shellTimeline}`} />
            </div>
          </div>
          <div className={styles.pane}>
            <div className={dashboard.panel}>
              <span className={`${dashboard.placeholder} ${dashboard.shellTitle}`} />
              <RowsSkeleton count={5} tall />
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}

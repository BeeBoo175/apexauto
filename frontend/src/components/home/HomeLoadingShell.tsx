import dashboard from "@/components/staff/DashboardSection.module.css";
import { RowsSkeleton } from "@/components/staff/DashboardSkeletons";
import styles from "./Home.module.css";
import attention from "./AttentionSection.module.css";

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
        <div className={attention.band}>
          <span className={`${dashboard.placeholder} ${dashboard.shellTitle}`} />
          <div className={attention.tiles}>
            {Array.from({ length: tiles }, (_, index) => (
              <div key={index} className={attention.tile}>
                <div className={attention.tileHead}>
                  <span className={`${dashboard.placeholder} ${attention.tileIcon}`} />
                  <span className={`${dashboard.placeholder} ${dashboard.shellTitle}`} />
                </div>
                <span className={`${dashboard.placeholder} ${attention.shellTile}`} />
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

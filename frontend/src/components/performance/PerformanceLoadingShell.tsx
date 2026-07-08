import type { ReactNode } from "react";
import loading from "@/components/staff/StaffLoadingShell.module.css";
import { ChartSkeleton, RowsSkeleton, StatsSkeleton } from "./PerformanceSkeletons";
import styles from "./Performance.module.css";

function ShellPanel({ half = false, children }: { half?: boolean; children: ReactNode }) {
  return (
    <div className={`${styles.panel} ${half ? styles.half : ""}`.trim()}>
      <div className={styles.panelHead}>
        <span className={`${styles.placeholder} ${styles.shellTitle}`} />
      </div>
      {children}
    </div>
  );
}

// Every tab opens with its headline figures. Branches then draw one line chart with a row per branch beneath it; the
// team views draw two charts side by side above one table of people.
function TeamOrBranchShell({ branches }: { branches: boolean }) {
  return (
    <>
      <ShellPanel>
        <StatsSkeleton />
      </ShellPanel>
      {branches ? (
        <ShellPanel>
          <ChartSkeleton />
          <RowsSkeleton count={3} />
        </ShellPanel>
      ) : (
        <>
          <div className={styles.grid}>
            <ShellPanel half>
              <ChartSkeleton />
            </ShellPanel>
            <ShellPanel half>
              <ChartSkeleton />
            </ShellPanel>
          </div>
          <ShellPanel>
            <RowsSkeleton count={4} tall />
          </ShellPanel>
        </>
      )}
    </>
  );
}

// The first paint of the requested view, from the same frame classes as the loaded page: Results with its headline,
// trend and stock by stage; the team and branch views with their chart.
export function PerformanceLoadingShell({ view, general }: { view: string; general: boolean }) {
  const current = view || "results";
  return (
    <div className={styles.workspace} aria-hidden="true">
      <div className={styles.shellToolbar}>
        <span className={`${styles.placeholder} ${styles.shellControl}`} />
      </div>
      <div className={loading.tabsRow}>
        {Array.from({ length: general ? 4 : 3 }, (_, index) => (
          <span key={index} className={loading.tabBar} />
        ))}
      </div>
      <div className={styles.view}>
        {current === "results" ? (
          <>
            <ShellPanel>
              <StatsSkeleton />
            </ShellPanel>
            <div className={styles.grid}>
              <ShellPanel half>
                <ChartSkeleton />
              </ShellPanel>
              <ShellPanel half>
                <ChartSkeleton />
              </ShellPanel>
            </div>
            <ShellPanel>
              <RowsSkeleton count={6} />
            </ShellPanel>
          </>
        ) : (
          <TeamOrBranchShell branches={current === "branches"} />
        )}
      </div>
    </div>
  );
}

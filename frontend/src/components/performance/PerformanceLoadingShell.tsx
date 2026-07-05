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

// Branches: four dumbbell panels. Team views: the completed-work matrix, then elapsed days beside work held now.
function TeamOrBranchShell({ branches }: { branches: boolean }) {
  if (branches) {
    return (
      <ShellPanel>
        <div className={styles.multiples}>
          {[0, 1, 2, 3].map((index) => (
            <div key={index} className={styles.multiple}>
              <span className={`${styles.placeholder} ${styles.shellTitle}`} />
              <RowsSkeleton count={3} />
            </div>
          ))}
        </div>
      </ShellPanel>
    );
  }
  return (
    <>
      <ShellPanel>
        <RowsSkeleton count={4} tall />
      </ShellPanel>
      <div className={styles.grid}>
        <ShellPanel half>
          <RowsSkeleton count={3} />
        </ShellPanel>
        <ShellPanel half>
          <RowsSkeleton count={3} />
        </ShellPanel>
      </div>
    </>
  );
}

// The first paint of the requested view, from the same frame classes as the loaded page: Results with its headline,
// trend and two now lists; the team and branch views with their measure bars.
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
            <div className={styles.grid}>
              <ShellPanel half>
                <RowsSkeleton count={6} />
              </ShellPanel>
              <ShellPanel half>
                <RowsSkeleton count={5} />
              </ShellPanel>
            </div>
          </>
        ) : (
          <TeamOrBranchShell branches={current === "branches"} />
        )}
      </div>
    </div>
  );
}

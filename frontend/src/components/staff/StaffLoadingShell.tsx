import { Card } from "@/components/ui/Card";
import { HomeLoadingShell } from "@/components/home/HomeLoadingShell";
import { TechnicianLoadingShell } from "@/components/inspection/TechnicianLoadingShell";
import { PerformanceLoadingShell } from "@/components/performance/PerformanceLoadingShell";
import { WorkCalendarLoadingShell } from "./WorkCalendarLoadingShell";
import { StaffTableLoadingShell } from "./StaffTableLoadingShell";
import layout from "./Staff.module.css";
import styles from "./StaffLoadingShell.module.css";

export type StaffLoadingView =
  | "calendar"
  | "intake"
  | "inspection"
  | "technician"
  | "repairs"
  | "appointment"
  | "report"
  | "home";

// The report shell follows the requested performance view and whether the actor also sees the Branches tab.
type LoadingShellProps = {
  view: StaffLoadingView;
  selected?: boolean;
  report?: { view: string; general: boolean; agent: boolean };
};

export function StaffLoadingShell({ view, selected, report }: LoadingShellProps) {
  return (
    <div className={styles.shell} role="status" aria-label="Loading workspace content">
      <span className="sr-only">Loading workspace content…</span>
      <LoadingContent view={view} selected={selected} report={report} />
    </div>
  );
}

function LoadingContent({ view, selected = false, report }: LoadingShellProps) {
  if (view === "technician") {
    return <TechnicianLoadingShell selected={selected} />;
  }
  if (view === "report") {
    return <PerformanceLoadingShell view={report?.view ?? ""} general={report?.general ?? false} />;
  }
  if (view === "home") {
    return <HomeLoadingShell tiles={report?.agent ? 3 : 4} toolbar={report?.general ?? false} />;
  }
  if (view === "calendar") {
    return <WorkCalendarLoadingShell />;
  }
  if (view === "appointment") {
    return (
      <div className={layout.direct}>
        <span className={`${layout.backLink} ${styles.shortValue}`} aria-hidden="true" />
        <Card>
          <div className={styles.detailBlocks} aria-hidden="true">
            <span className={styles.monthTitle} />
            <span className={styles.block} />
            <span className={styles.block} />
          </div>
        </Card>
      </div>
    );
  }
  return <StaffTableLoadingShell />;
}

import { Card } from "@/components/ui/Card";
import { TechnicianLoadingShell } from "@/components/inspection/TechnicianLoadingShell";
import { IntakeCalendarLoadingShell } from "./IntakeCalendarLoadingShell";
import { StaffTableLoadingShell } from "./StaffTableLoadingShell";
import layout from "./Staff.module.css";
import styles from "./StaffLoadingShell.module.css";

export type StaffLoadingView =
  "calendar" | "intake" | "inspection" | "technician" | "repairs" | "appointment";

type LoadingShellProps = { view: StaffLoadingView; selected?: boolean };

export function StaffLoadingShell({ view, selected }: LoadingShellProps) {
  return (
    <div className={styles.shell} role="status" aria-label="Loading workspace content">
      <span className="sr-only">Loading workspace content…</span>
      <LoadingContent view={view} selected={selected} />
    </div>
  );
}

function LoadingContent({ view, selected = false }: LoadingShellProps) {
  if (view === "technician") {
    return <TechnicianLoadingShell selected={selected} />;
  }
  if (view === "calendar") {
    return <IntakeCalendarLoadingShell />;
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

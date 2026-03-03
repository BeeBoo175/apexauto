import { Card } from "@/components/ui/Card";
import { SignedReportLoadingShell } from "@/components/inspection/SignedReportLoadingShell";
import { IntakeCalendarLoadingShell } from "./IntakeCalendarLoadingShell";
import { StaffTableLoadingShell } from "./StaffTableLoadingShell";
import layout from "./Staff.module.css";
import styles from "./StaffLoadingShell.module.css";

export type StaffLoadingView =
  "calendar" | "intake" | "inspection" | "repairs" | "appointment" | "report";

export function StaffLoadingShell({ view }: { view: StaffLoadingView }) {
  return (
    <div className={styles.shell} role="status" aria-label="Loading workspace content">
      <span className="sr-only">Loading workspace content…</span>
      <LoadingContent view={view} />
    </div>
  );
}

function LoadingContent({ view }: { view: StaffLoadingView }) {
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
  if (view === "report") {
    return <SignedReportLoadingShell />;
  }
  return <StaffTableLoadingShell />;
}

import type { ReactNode } from "react";
import workbench from "./StaffWorkspace.module.css";
import styles from "./StaffHistory.module.css";

// The frame every record history shares: the "Record history" heading, an empty message, the timeline and its
// paging or recovery controls. Each history supplies only its events.
export function StaffHistorySection({
  label,
  busy,
  empty,
  progress,
  children,
}: {
  label: string;
  busy: boolean;
  empty?: ReactNode;
  progress: ReactNode;
  children: ReactNode;
}) {
  return (
    <section className={styles.history} aria-label={label} aria-busy={busy}>
      <div className={workbench.sectionHeader}>
        <h2 className={workbench.sectionTitle}>Record history</h2>
      </div>
      {empty}
      {children}
      {progress}
    </section>
  );
}

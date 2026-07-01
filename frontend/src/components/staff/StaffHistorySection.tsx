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
  flush = false,
  children,
}: {
  label: string;
  busy: boolean;
  empty?: ReactNode;
  progress: ReactNode;
  // Drawers whose other tabs start at the top edge drop the extra top space.
  flush?: boolean;
  children: ReactNode;
}) {
  return (
    <section
      className={`${styles.history} ${flush ? styles.flush : ""}`.trim()}
      aria-label={label}
      aria-busy={busy}
    >
      <div className={workbench.sectionHeader}>
        <h2 className={workbench.sectionTitle}>Record history</h2>
      </div>
      {empty}
      {children}
      {progress}
    </section>
  );
}

import { StaffSummaryLoadingShell } from "./StaffSummaryLoadingShell";
import styles from "./StaffDrawerLoadingShell.module.css";

export function StaffDrawerLoadingShell({
  label = "Loading record…",
  section,
  fact = false,
  single = false,
  footer = true,
  tabs = true,
}: {
  label?: string;
  section?: string;
  // The summary carries a labelled fact under its headline, such as the handling agent.
  fact?: boolean;
  // One content column instead of an index beside the detail.
  single?: boolean;
  footer?: boolean;
  tabs?: boolean;
}) {
  const oneColumn = single || section === "history" || section === "repairs";
  return (
    <>
      <p role="status" className="sr-only">
        {label}
      </p>
      <div
        className={styles.shell}
        data-layout={oneColumn ? "single" : undefined}
        aria-hidden="true"
      >
        <StaffSummaryLoadingShell fact={fact} />
        {tabs && (
          <div className={styles.sections}>
            <span className={styles.line} />
            <span className={styles.line} />
            <span className={styles.line} />
          </div>
        )}
        <div className={styles.body}>
          <span className={styles.index} />
          <div className={styles.detail}>
            <span className={styles.title} />
            <span className={styles.block} />
            <span className={styles.block} />
          </div>
        </div>
        {footer && (
          <div className={styles.footer}>
            <span className={styles.line} />
          </div>
        )}
      </div>
    </>
  );
}

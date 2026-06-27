import workbench from "./StaffWorkspace.module.css";
import styles from "./StaffSummaryLoadingShell.module.css";

// The loading shape of StaffRecordSummary: label, headline, optional fact, status badge and one context line.
export function StaffSummaryLoadingShell({ fact = false }: { fact?: boolean }) {
  return (
    <div className={workbench.summary} aria-hidden="true">
      <div className={workbench.summaryMain}>
        <div>
          <span className={`${styles.placeholder} ${styles.label}`} />
          <span className={`${styles.placeholder} ${styles.value}`} />
          {fact && (
            <div className={styles.fact}>
              <span className={`${styles.placeholder} ${styles.factLabel}`} />
              <span className={`${styles.placeholder} ${styles.factValue}`} />
            </div>
          )}
        </div>
        <div className={workbench.summaryContext}>
          <span className={`${styles.placeholder} ${styles.status}`} />
          <span className={`${styles.placeholder} ${styles.context}`} />
        </div>
      </div>
    </div>
  );
}

import shimmer from "@/components/staff/StaffRecordLoadingShell.module.css";
import styles from "./SaleDocuments.module.css";

export function SaleDocumentsLoadingShell() {
  return (
    <div>
      <p role="status" className="sr-only">
        Loading sales documents…
      </p>
      <div className={styles.loading} aria-hidden="true">
        {[0, 1, 2].map((row) => (
          <div key={row} className={styles.loadingRow}>
            <span className={`${shimmer.placeholder} ${styles.loadingTitle}`} />
            <span className={`${shimmer.placeholder} ${styles.loadingDetail}`} />
          </div>
        ))}
      </div>
    </div>
  );
}

import { StaffSummaryLoadingShell } from "@/components/staff/StaffSummaryLoadingShell";
import type { ListingSection } from "@/lib/vehicles/listings";
import styles from "./ListingLoadingShell.module.css";

export function ListingLoadingShell({ section }: { section: ListingSection }) {
  const editing = section === "listing" || section === "card";
  return (
    <div className={styles.shell} aria-hidden="true">
      <StaffSummaryLoadingShell />
      {editing ? (
        <>
          <div className={styles.toolbar}>
            <span className={styles.line} />
          </div>
          <div className={styles.split} data-card={section === "card" || undefined}>
            <div className={styles.fields}>
              <span className={styles.heading} />
              <span className={styles.field} />
              <span className={styles.field} />
              <span className={styles.description} />
              <span className={styles.field} />
            </div>
            <div className={styles.preview}>
              <span className={styles.photo} />
              <span className={styles.heading} />
              <span className={styles.line} />
              <span className={styles.field} />
            </div>
          </div>
        </>
      ) : (
        <div className={styles.records}>
          <span className={styles.heading} />
          <span className={styles.description} />
          <span className={styles.description} />
        </div>
      )}
      <div className={styles.footer}>
        <span className={styles.line} />
      </div>
    </div>
  );
}

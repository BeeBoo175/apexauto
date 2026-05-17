import styles from "./SignedReport.module.css";
import loading from "@/components/staff/StaffLoadingShell.module.css";

export function SignedReportLoadingShell() {
  return (
    <article className={styles.report} aria-hidden="true">
      <header className={styles.heading}>
        <span className={loading.monthTitle} />
        <span className={loading.grade} />
      </header>
      <div className={styles.identity}>
        <span className={loading.value} />
        <span className={loading.value} />
        <span className={loading.value} />
        <span className={loading.value} />
      </div>
      <div className={loading.detailBlocks}>
        <span className={loading.block} />
        <span className={loading.block} />
        <span className={loading.block} />
      </div>
    </article>
  );
}

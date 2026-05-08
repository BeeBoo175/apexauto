import styles from "./StaffDrawerLoadingShell.module.css";

export function StaffDrawerLoadingShell({
  label = "Loading record…",
  section,
}: {
  label?: string;
  section?: string;
}) {
  return (
    <>
      <p role="status" className="sr-only">
        {label}
      </p>
      <div className={styles.shell} data-section={section} aria-hidden="true">
        <div className={styles.summary}>
          <div className={styles.summaryMain}>
            <div className={styles.estimate}>
              <span className={styles.line} />
              <span className={styles.title} />
            </div>
            <span className={styles.context} />
          </div>
          <div className={styles.details}>
            <span className={styles.line} />
            <span className={styles.line} />
          </div>
        </div>
        <div className={styles.sections}>
          <span className={styles.line} />
          <span className={styles.line} />
          <span className={styles.line} />
        </div>
        <div className={styles.body}>
          <span className={styles.index} />
          <div className={styles.detail}>
            <span className={styles.title} />
            <span className={styles.block} />
            <span className={styles.block} />
          </div>
        </div>
        <div className={styles.footer}>
          <span className={styles.line} />
        </div>
      </div>
    </>
  );
}
